using System;
using System.Collections.Concurrent;
using ManagedBass;
using ManagedBass.Fx;
using UnityEngine;
using YARG.Audio.BASS.Effects;
using YARG.Audio.PitchDetection;
using YARG.Core.Logging;
using YARG.Core.Audio;
using YARG.Input;
using YARG.Settings;

namespace YARG.Audio.BASS
{
    internal class MonitorPlaybackHandle : IDisposable
    {
#nullable enable
        public static MonitorPlaybackHandle? Create(int sampleRate)
#nullable disable
        {
            // Set up monitoring stream
            int monitorPlaybackHandle = Bass.CreateStream(sampleRate, 1, BassFlags.Default, StreamProcedureType.Push);
            if (monitorPlaybackHandle == 0)
            {
                YargLogger.LogFormatError("Failed to create monitor stream: {0}!", Bass.LastError);
                return null;
            }

            // Add reverb to the monitor playback
            var reverb = BassFreeverbDsp.Create(monitorPlaybackHandle,
                dryMix: 0.3f,
                wetMix: 1f,
                roomSize: 0.4f,
                damp: 0.7f,
                width: 0f,
                priority: 1);
            if (reverb == null)
            {
                YargLogger.LogError("Failed to add reverb to monitor stream!");
                Bass.StreamFree(monitorPlaybackHandle);
                return null;
            }

            // Apply gain to the playback
            var gain = BassGainDsp.Attach(monitorPlaybackHandle, 1.3f);
            if (gain == null)
            {
                YargLogger.LogError("Failed to add native gain to monitor stream!");
                reverb.Dispose();
                Bass.StreamFree(monitorPlaybackHandle);
                return null;
            }

            // Start monitoring
            if (!Bass.ChannelPlay(monitorPlaybackHandle))
            {
                YargLogger.LogFormatError("Failed to start monitor stream: {0}!", Bass.LastError);
                gain.Dispose();
                reverb.Dispose();
                Bass.StreamFree(monitorPlaybackHandle);
                return null;
            }

            return new MonitorPlaybackHandle(monitorPlaybackHandle, reverb, gain);
        }

        public readonly  int             Handle;
        private readonly BassFreeverbDsp _reverb;
        private readonly BassGainDsp     _gain;

        private bool _disposed;

        private MonitorPlaybackHandle(int handle, BassFreeverbDsp reverb, BassGainDsp gain)
        {
            Handle = handle;
            _reverb = reverb;
            _gain = gain;
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _reverb.Dispose();
                _gain.Dispose();
                Bass.StreamFree(Handle);
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~MonitorPlaybackHandle()
        {
            Dispose(false);
        }

        public void ResetReverb()
        {
            _reverb.RequestReset();
        }

        public int ResetBuffer()
        {
            Bass.StreamPutData(Handle, IntPtr.Zero, 0);

            // Restarting a push stream flushes both its playback buffer and unbounded
            // queue. Resetting position alone does not reliably clear both on all backends.
            if (!Bass.ChannelPlay(Handle, true))
            {
                return (int) Bass.LastError;
            }

            ResetReverb();
            return 0;
        }
    }

    internal class RecordingHandle : IDisposable
    {
        private static readonly int[] _sampleRates =
        {
            48000,
            44100,
            96000,
            16000
        };

#nullable enable
        public static RecordingHandle? CreateRecordingHandle(RecordProcedure procedure, int channels = 1)
#nullable disable
        {
            var devPeriod = Bass.GetConfig(Configuration.DevicePeriod);
            foreach (int sampleRate in _sampleRates)
            {
                int handle = Bass.RecordStart(sampleRate, channels, BassFlags.RecordPause, devPeriod, procedure,
                    IntPtr.Zero);
                if (handle == 0)
                {
                    YargLogger.LogFormatTrace("Failed to start clean recording at {0} Hz / {1} ch: {2}!", sampleRate,
                        channels, Bass.LastError);
                    continue;
                }

                return new RecordingHandle(handle, devPeriod, sampleRate, channels);
            }

            YargLogger.LogError("Failed to start recording at any supported sample rate!");
            return null;
        }

        public readonly int Handle;
        public readonly int RecordPeriod;
        public readonly int SampleRate;

        public readonly int Channels;

        private bool _disposed;

        private RecordingHandle(int handle, int period, int sampleRate, int channels)
        {
            Handle = handle;
            RecordPeriod = period;
            SampleRate = sampleRate;
            Channels = channels;
        }

        public bool Start()
        {
            return Bass.ChannelPlay(Handle);
        }

        public bool Pause()
        {
            return Bass.ChannelPause(Handle);
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                Bass.ChannelStop(Handle);
                Bass.StreamFree(Handle);
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~RecordingHandle()
        {
            Dispose(false);
        }
    }

    public sealed class BassMicDevice : MicDevice
    {
        private const float MIC_HIT_INPUT_THRESHOLD = 25f;

#nullable enable
        internal static BassMicDevice? Create(int deviceId, string baseName, RecordingSession session,
            int captureChannel = 0)
#nullable disable
        {
            string displayName = session.Channels > 1 ? $"{baseName} - Channel {captureChannel + 1}" : baseName;
            var device = new BassMicDevice(deviceId, baseName, displayName, session, captureChannel);

            device._processedHandle =
                Bass.CreateStream(session.SampleRate, 1, BassFlags.Decode, StreamProcedureType.Push);
            if (device._processedHandle == 0)
            {
                YargLogger.LogFormatError("Failed to create processed recording stream for mic '{0}': {1}!",
                    displayName, Bass.LastError);
                return null;
            }

            device._pitchDetector = new PitchTracker(sampleRate: session.SampleRate);

            var monitorPlayback = MonitorPlaybackHandle.Create(session.SampleRate);
            if (monitorPlayback == null)
            {
                Bass.StreamFree(device._processedHandle);
                return null;
            }

            device._monitorHandle = monitorPlayback;

            int lowEqHandle = BassHelpers.AddEqToChannel(device._processedHandle, _lowEqParameters);
            int highEqHandle = BassHelpers.AddEqToChannel(device._processedHandle, _highEqParameters);
            if (lowEqHandle == 0 || highEqHandle == 0)
            {
                YargLogger.LogFormatError("Failed to add EQ to processed recording stream: {0}!", Bass.LastError);
                device.Dispose();
                return null;
            }

            session.AddMic(device);
            return device;
        }

        internal event Action Disposed;

        private static readonly PeakEQParameters _lowEqParameters = new()
        {
            fBandwidth = 2.5f,
            fCenter = 20f,
            fGain = -10f
        };

        private static readonly PeakEQParameters _highEqParameters = new()
        {
            fBandwidth = 2.5f,
            fCenter = 10_000f,
            fGain = -10f
        };

        private float? _lastPitch;
        private float? _lastAmplitude;

        private readonly ConcurrentQueue<MicOutputFrame> _frameQueue = new();

        private PitchTracker _pitchDetector;

        private MonitorPlaybackHandle _monitorHandle;

        private readonly string           _baseName;
        private readonly int              _deviceId;
        private readonly int              _captureChannel;
        private readonly RecordingSession _session;

        private int _processedHandle;

        private int _timeAccumulated;
        private int _processedBufferLength;

        internal int CaptureChannel => _captureChannel;

        public override int Reset()
        {
            _frameQueue.Clear();

            // This is a little bit ugly but I think this is the only way to clear the processing buffer.
            // You can't request the available bytes from a decoding channel so there's no way to know how much data is available.
            // And you can't just request as much data as possible into a NULL buffer because that only works for recording streams.
            // So we have to allocate a buffer and keep requesting data until there's none left.
            unsafe
            {
                const int bufferLength = 1024;

                byte* buffer = stackalloc byte[bufferLength];
                int bytesRead;
                do
                {
                    bytesRead = Bass.ChannelGetData(_processedHandle, (IntPtr) buffer, bufferLength);
                    if (bytesRead >= 0)
                    {
                        YargLogger.LogFormatTrace("Cleared {0} bytes from processed recording buffer", bytesRead);
                    }
                } while (bytesRead > 0);

                if (bytesRead == -1)
                {
                    YargLogger.LogFormatError("Failed to clear processed recording buffer: {0}!", Bass.LastError);
                    return (int) Bass.LastError;
                }
            }

            int monitorError = _monitorHandle.ResetBuffer();
            if (monitorError != 0)
            {
                return monitorError;
            }

            _session.FlushRecordBuffer();

            return 0;
        }

        public override bool DequeueOutputFrame(out MicOutputFrame frame)
        {
            return _frameQueue.TryDequeue(out frame);
        }

        public override void ClearOutputQueue()
        {
            _frameQueue.Clear();
        }

        public override void SetMonitoringLevel(float volume)
        {
            if (!Bass.ChannelSetAttribute(_monitorHandle.Handle, ChannelAttribute.Volume, volume))
            {
                YargLogger.LogFormatError("Failed to set volume attribute: {0}", Bass.LastError);
            }
        }

        public override SerializedMic Serialize()
        {
            return new SerializedMic(_baseName, _captureChannel);
        }

        private BassMicDevice(int deviceId, string baseName, string displayName, RecordingSession session, int captureChannel)
            : base(displayName)
        {
            _baseName = baseName;
            _deviceId = deviceId;
            _captureChannel = captureChannel;
            _session = session;
        }

        /// <summary>
        /// Handles incoming audio from the BASS recording callback.
        /// </summary>
        /// <remarks>
        /// Most mics are mono. But a single physical device
        /// (like a USB audio interface with 2 XLR inputs) can appear as one stereo device.
        /// BASS then delivers interleaved 16-bit samples: [mic1, mic2, mic1, mic2...].
        /// <para>
        /// Each mic is assigned its own channel index (<see cref="_captureChannel"/>).
        /// This method extracts just the selected channel from the interleaved frame and converts it to mono
        /// </para>
        /// <para>
        /// If the device is already mono, no copy is made. Otherwise a temporary mono buffer
        /// is allocated on the stack (no garbage collection) for the current callback only.
        /// This is safe because the stack memory lives until this method returns, is wrapped
        /// in a <see cref="Span{T}"/> so the compiler prevents it from escaping, and is
        /// passed immediately to <see cref="PushMonoData"/> via <c>fixed</c> without being
        /// stored. The size is also tiny — roughly 1 KB per callback (e.g. at 48 kHz
        /// with the default 10 ms BASS device period: 48,000 * 0.01 s = 480 frames,
        /// 480 * 2 bytes = 960 bytes) — so it will not overflow the thread stack (~256 KB–1 MB).
        /// </para>
        /// </remarks>
        /// <param name="buffer">Pointer to the interleaved 16-bit PCM data from BASS.</param>
        /// <param name="length">Size of <paramref name="buffer"/> in bytes.</param>
        internal void ProcessData(IntPtr buffer, int length)
        {
            if (length <= 0)
            {
                return;
            }

            int channels = _session.Channels;
            if (channels <= 1)
            {
                PushMonoData(buffer, length);
                return;
            }

            int frames = length / (channels * sizeof(short));
            if (frames <= 0)
            {
                return;
            }

            Span<short> mono = stackalloc short[frames];
            unsafe
            {
                short* src = (short*) buffer;
                if (_captureChannel >= channels)
                {
                    YargLogger.LogFormatError(
                        "Mic '{0}' capture channel {1} exceeds session channel count {2}",
                        DisplayName, _captureChannel, channels);
                    return;
                }

                for (int i = 0; i < frames; ++i)
                {
                    mono[i] = src[i * channels + _captureChannel];
                }
            }

            unsafe
            {
                fixed (short* ptr = mono)
                {
                    PushMonoData((IntPtr) ptr, frames * sizeof(short));
                }
            }
        }

        /// <summary>
        /// Sends mono audio to the monitor stream and to the processing stream.
        /// Buffers audio until enough time has passed to run pitch detection.
        /// </summary>
        private void PushMonoData(IntPtr monoBuffer, int monoLength)
        {
            // Copies the data from the recording buffer to the monitor playback buffer.
            if (Bass.StreamPutData(_monitorHandle.Handle, monoBuffer, monoLength) == -1)
            {
                YargLogger.LogFormatError("Error pushing data to monitor stream: {0}", Bass.LastError);
            }

            // Wait for initialization to complete before processing data
            if (!IsRecordingOutput)
            {
                return;
            }

            // Copy the data to the batch handle to apply FX
            Bass.StreamPutData(_processedHandle, monoBuffer, monoLength);

            _timeAccumulated += _session.RecordPeriod;

            _processedBufferLength += monoLength;

            // Enough time has passed for pitch detection
            if (_timeAccumulated >= RECORD_PERIOD_MS)
            {
                unsafe
                {
                    Span<byte> procBuff = stackalloc byte[_processedBufferLength];
                    fixed (byte* ptr = procBuff)
                    {
                        Bass.ChannelGetData(_processedHandle, (IntPtr) ptr, _processedBufferLength);
                        int shortLength = _processedBufferLength / sizeof(short);
                        var readOnlySpan = new ReadOnlySpan<short>(ptr, shortLength);
                        CalculatePitchAndAmplitude(readOnlySpan);
                    }
                }

                _timeAccumulated = 0;
                _processedBufferLength = 0;
            }
        }

        private void CalculatePitchAndAmplitude(ReadOnlySpan<short> buffer)
        {
            int sampleCount = buffer.Length;
            Span<float> floatBuffer = stackalloc float[sampleCount];

            // Convert 16 bit buffer to floats
            // If this isn't 16 bit god knows what device they're using.
            for (int i = 0; i < sampleCount; i++)
            {
                floatBuffer[i] = buffer[i] / 32768f;
            }

            // Calculate the root mean square
            float sum = 0f;
            int count = 0;
            for (int i = 0; i < sampleCount; i += 4, count++)
            {
                sum += floatBuffer[i] * floatBuffer[i];
            }

            sum = Mathf.Sqrt(sum / count);

            // Convert to decibels to get the amplitude
            float amplitude = 20f * Mathf.Log10(sum * 180f);
            if (amplitude < -160f)
            {
                amplitude = -160f;
            }

            // Detect peaks for hit inputs
            if (amplitude > _lastAmplitude && Mathf.Abs(amplitude - _lastAmplitude.Value) >= MIC_HIT_INPUT_THRESHOLD)
            {
                var hitFrame = new MicOutputFrame(InputManager.CurrentInputTime, true, -1f, -1f);
                _frameQueue.Enqueue(hitFrame);
            }

            _lastAmplitude = amplitude;

            // Skip pitch detection if not speaking
            if (amplitude < SettingsManager.Settings.MicrophoneSensitivity.Value)
            {
                _lastPitch = null;
                return;
            }

            // Process the pitch buffer
            var pitchOutput = _pitchDetector.ProcessBuffer(floatBuffer);
            if (pitchOutput != null)
            {
                _lastPitch = pitchOutput;
            }

            // We cannot push a frame if there was no pitch
            if (_lastPitch == null)
            {
                return;
            }

            // Queue a MicOutput frame
            var frame = new MicOutputFrame(InputManager.CurrentInputTime, false,
                _lastPitch.Value, amplitude);
            _frameQueue.Enqueue(frame);
        }

        private void ResetProcessingState()
        {
            _timeAccumulated = 0;
            _processedBufferLength = 0;
            _lastPitch = null;
            _lastAmplitude = null;
            _frameQueue.Clear();
            _pitchDetector?.Reset();
        }

        public void RestartRecording()
        {
            // Keep the native recording channel alive across scene/song transitions.
            // Rapidly closing and reopening the capture device can succeed without the
            // backend delivering any callbacks. Pausing also prevents new monitor data
            // from arriving while its queue and processing state are reset.
            if (!_session.Pause())
            {
                YargLogger.LogFormatError("Failed to pause BASS recording stream for mic '{0}': {1}!",
                    DisplayName, Bass.LastError);
                return;
            }

            ResetProcessingState();
            _session.FlushRecordBuffer();

            int monitorError = _monitorHandle.ResetBuffer();
            if (monitorError != 0)
            {
                YargLogger.LogFormatError("Failed to reset monitor stream for mic '{0}': {1}!",
                    DisplayName, (Errors) monitorError);
            }

            if (!_session.Start())
            {
                YargLogger.LogFormatError("Failed to resume BASS recording stream for mic '{0}': {1}!",
                    DisplayName, Bass.LastError);
                return;
            }
        }

        protected override void DisposeUnmanagedResources()
        {
            _session.RemoveMic(this);
            Disposed?.Invoke();

            _monitorHandle?.Dispose();
            if (_processedHandle != 0)
            {
                Bass.StreamFree(_processedHandle);
                _processedHandle = 0;
            }
        }
    }
}