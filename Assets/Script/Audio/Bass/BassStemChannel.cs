using System;
using System.IO;
using Cysharp.Threading.Tasks;
using ManagedBass;
using ManagedBass.Fx;
using ManagedBass.Mix;
using UnityEngine;
using YARG.Core.Audio;

namespace YARG.Audio.BASS
{
    public sealed class BassStemChannel : IStemChannel<BassAudioManager>
    {
        private struct Handles : IDisposable
        {
            public int Stream;

            public int CompressorFX;
            public int PitchFX;
            public int ReverbFX;

            public int LowEQ;
            public int MidEQ;
            public int HighEQ;

            public static bool Create(int sourceStream, double volume, int[] indices, out Handles handles)
            {
                const BassFlags splitFlags = BassFlags.Decode | BassFlags.SplitPosition;
                const BassFlags tempoFlags = BassFlags.SampleOverrideLowestVolume | BassFlags.Decode | BassFlags.FxFreeSource;

                handles = default;
#nullable enable
                int[]? channelMap = null;
#nullable disable
                if (indices != null)
                {
                    channelMap = new int[indices.Length + 1];
                    for (int i = 0; i < indices.Length; ++i)
                    {
                        channelMap[i] = indices[i];
                    }
                    channelMap[indices.Length] = -1;
                }

                int streamSplit = BassMix.CreateSplitStream(sourceStream, splitFlags, channelMap);
                if (streamSplit == 0)
                {
                    Debug.LogError($"Failed to create split stream: {Bass.LastError}");
                    return false;
                }

                handles.Stream = BassFx.TempoCreate(streamSplit, tempoFlags);
                if (!Bass.ChannelSetAttribute(handles.Stream, ChannelAttribute.Volume, volume))
                {
                    Debug.LogError($"Failed to set channel volume: {Bass.LastError}");
                }

                handles.CompressorFX = BassHelpers.AddCompressorToChannel(handles.Stream);
                if (handles.CompressorFX == 0)
                {
                    Debug.LogError($"Failed to set up compressor for split stream!");
                }
                return true;
            }

            public void Dispose()
            {
                // FX handles are freed automatically, we only need to free the stream
                if (Stream != 0)
                {
                    if (!Bass.StreamFree(Stream))
                        Debug.LogError($"Failed to free channel stream (THIS WILL LEAK MEMORY!): {Bass.LastError}");
                    Stream = 0;
                }
            }
        }

        private int _channelEndHandle;
        private event Action _channelEnd;

        public event Action ChannelEnd
        {
            add
            {
                if (_channelEndHandle == 0)
                {
                    SyncProcedure sync = (_, _, _, _) =>
                    {
                        // Prevent potential race conditions by caching the value as a local
                        var end = _channelEnd;
                        if (end != null)
                        {
                            UnityMainThreadCallback.QueueEvent(end.Invoke);
                        }
                    };
                    _channelEndHandle = IsMixed
                        ? BassMix.ChannelSetSync(StreamHandle, SyncFlags.End, 0, sync)
                        : Bass.ChannelSetSync(StreamHandle, SyncFlags.End, 0, sync);
                }

                _channelEnd += value;
            }
            remove { _channelEnd -= value; }
        }

        private double _lastStemVolume;

        private int _sourceHandle;

        private Handles _streamHandles;
        private Handles _reverbHandles;

        private bool _isReverbing;
        private bool _disposed;

        public SongStem Stem { get; }
        public double LengthD { get; private set; }

        public double Volume { get; private set; }

        public int StreamHandle => _streamHandles.Stream;
        public int ReverbStreamHandle => _reverbHandles.Stream;

        public bool IsMixed { get; set; } = false;

		private PitchShiftParametersStruct _pitchParams;

#nullable enable
        public static BassStemChannel? CreateChannel(IAudioManager manager, Stream stream, SongStem stem, float speed, int[] indices)
#nullable disable
        {
            if (!CreateSourceStream(stream, out int sourceStream))
            {
                return null;
            }

            double volume = manager.GetVolumeSetting(stem);
            if (!CreateSplitStreams(sourceStream, volume, indices, out var streamHandles, out var reverbHandles))
            {
                return null;
            }

            var pitchparams = SetPitchParams(manager.Options, stem, speed, ref streamHandles, ref reverbHandles);
            return new BassStemChannel(stem, volume, sourceStream, pitchparams, streamHandles, reverbHandles);
        }

        private BassStemChannel(SongStem stem, double stemVolume, int sourceStream, in PitchShiftParametersStruct pitchParams, in Handles streamHandles, in Handles reverbHandles)
        {
            Stem = stem;
            _sourceHandle = sourceStream;
            _streamHandles = streamHandles;
            _reverbHandles = reverbHandles;
            _lastStemVolume = stemVolume;
            _pitchParams = pitchParams;

            LengthD = GetLengthInSeconds();
            Volume = 1;
        }

        ~BassStemChannel()
        {
            Dispose(false);
        }

        public void FadeIn(float maxVolume)
        {
            Bass.ChannelSetAttribute(StreamHandle, ChannelAttribute.Volume, 0);
            Bass.ChannelSlideAttribute(StreamHandle, ChannelAttribute.Volume, maxVolume,
                BassHelpers.FADE_TIME_MILLISECONDS);
        }

        public UniTask FadeOut()
        {
            Bass.ChannelSlideAttribute(StreamHandle, ChannelAttribute.Volume, 0, BassHelpers.FADE_TIME_MILLISECONDS);
            return UniTask.WaitUntil(() =>
            {
                Bass.ChannelGetAttribute(StreamHandle, ChannelAttribute.Volume, out var currentVolume);
                return Mathf.Abs(currentVolume) <= 0.01f;
            });
        }

        public void SetVolume(BassAudioManager manager, double newVolume)
        {
            if (StreamHandle == 0)
            {
                return;
            }

            double volumeSetting = manager.GetVolumeSetting(Stem);

            double oldBassVol = _lastStemVolume * Volume;
            double newBassVol = volumeSetting * newVolume;

            // Limit minimum stem volume
            if (manager.Options.UseMinimumStemVolume)
            {
                newBassVol = Math.Max(newBassVol, AudioOptions.MINIMUM_STEM_VOLUME);
            }

            // Values are the same, no need to change
            if (Math.Abs(oldBassVol - newBassVol) < double.Epsilon)
            {
                return;
            }

            Volume = newVolume;
            _lastStemVolume = volumeSetting;

            if (!Bass.ChannelSetAttribute(StreamHandle, ChannelAttribute.Volume, newBassVol))
                Debug.LogError($"Failed to set stream volume: {Bass.LastError}");

            double reverbVolume = _isReverbing ? newBassVol * BassHelpers.REVERB_VOLUME_MULTIPLIER : 0;

            if (!Bass.ChannelSetAttribute(ReverbStreamHandle, ChannelAttribute.Volume, reverbVolume))
                Debug.LogError($"Failed to set reverb volume: {Bass.LastError}");
        }

        public void SetReverb(BassAudioManager manager, bool reverb)
        {
            _isReverbing = reverb;
            if (reverb)
            {
                // Reverb already applied
                if (_reverbHandles.ReverbFX != 0) return;

                // Set reverb FX
                _reverbHandles.LowEQ = BassHelpers.AddEqToChannel(ReverbStreamHandle, BassHelpers.LowEqParams);
                _reverbHandles.MidEQ = BassHelpers.AddEqToChannel(ReverbStreamHandle, BassHelpers.MidEqParams);
                _reverbHandles.HighEQ = BassHelpers.AddEqToChannel(ReverbStreamHandle, BassHelpers.HighEqParams);
                _reverbHandles.ReverbFX = BassHelpers.AddReverbToChannel(ReverbStreamHandle);

                double volumeSetting = manager.GetVolumeSetting(Stem);
                if (!Bass.ChannelSlideAttribute(ReverbStreamHandle, ChannelAttribute.Volume,
                    (float) (volumeSetting * Volume * BassHelpers.REVERB_VOLUME_MULTIPLIER), BassHelpers.REVERB_SLIDE_IN_MILLISECONDS))
                {
                    Debug.LogError($"Failed to set reverb volume: {Bass.LastError}");
                }
            }
            else
            {
                // No reverb is applied
                if (_reverbHandles.ReverbFX == 0) return;

                // Remove low-high
                if(!Bass.ChannelRemoveFX(ReverbStreamHandle, _reverbHandles.LowEQ) ||
                    !Bass.ChannelRemoveFX(ReverbStreamHandle, _reverbHandles.MidEQ) ||
                    !Bass.ChannelRemoveFX(ReverbStreamHandle, _reverbHandles.HighEQ) ||
                    !Bass.ChannelRemoveFX(ReverbStreamHandle, _reverbHandles.ReverbFX))
                {
                    Debug.LogError($"Failed to remove effects: {Bass.LastError}");
                }

                _reverbHandles.LowEQ = 0;
                _reverbHandles.MidEQ = 0;
                _reverbHandles.HighEQ = 0;
                _reverbHandles.ReverbFX = 0;

                if (!Bass.ChannelSlideAttribute(ReverbStreamHandle, ChannelAttribute.Volume, 0,
                    BassHelpers.REVERB_SLIDE_OUT_MILLISECONDS))
                {
                    Debug.LogError($"Failed to set reverb volume: {Bass.LastError}");
                }
            }
        }

        public void SetSpeed(float speed)
        {
            speed = (float) Math.Round(Math.Clamp(speed, 0.05, 50), 2);
            SetSpeed(speed, StreamHandle, ReverbStreamHandle);
        }

        public void SetWhammyPitch(BassAudioManager manager, float percent)
        {
            if (_streamHandles.PitchFX == 0 || _reverbHandles.PitchFX == 0)
                return;

            percent = Mathf.Clamp(percent, 0f, 1f);

            float shift = Mathf.Pow(2, -(manager.Options.WhammyPitchShiftAmount * percent) / 12);
            _pitchParams.fPitchShift = shift;

            if (!BassHelpers.FXSetParameters(_streamHandles.PitchFX, _pitchParams))
            {
                Debug.LogError("Failed to set params (normal fx): " + Bass.LastError);
            }

            if (!BassHelpers.FXSetParameters(_reverbHandles.PitchFX, _pitchParams))
            {
                Debug.LogError("Failed to set params (reverb fx): " + Bass.LastError);
            }
        }

        private double GetDesyncOffset(BassAudioManager manager, bool bufferCompensation = true)
        {
            // Playback buffer compensation is optional
            // All other desync compensation is always done
            double desync = bufferCompensation ? manager.PlaybackBufferLength : 0;

            // Hack to get desync of pitch-bent channels
            if (_streamHandles.PitchFX != 0 && _reverbHandles.PitchFX != 0)
            {
                // The desync is caused by the FFT window
                // BASS_FX does not account for it automatically so we must do it ourselves
                // (thanks Matt/Oscar for the info!)
                if (Bass.ChannelGetAttribute(StreamHandle, ChannelAttribute.Frequency, out float sampleRate))
                    desync += _pitchParams.FFTSize / sampleRate;
                else
                    Debug.LogError($"Failed to get sample rate: {Bass.LastError}");
            }

            return desync;
        }

        public double GetPosition(BassAudioManager manager, bool bufferCompensation = true)
        {
            // BassMix.ChannelGetPosition is very wonky when seeking
            // compared to Bass.ChannelGetPosition
            // We'll just have to make do with the less granular time reporting
            // long position = IsMixed
            //     ? BassMix.ChannelGetPosition(_streamHandles.Stream)
            //     : Bass.ChannelGetPosition(_streamHandles.Stream);
            long position = Bass.ChannelGetPosition(StreamHandle);
            if (position < 0)
            {
                Debug.LogError($"Failed to get channel position in bytes: {Bass.LastError}");
                return -1;
            }

            double seconds = Bass.ChannelBytes2Seconds(StreamHandle, position);
            if (seconds < 0)
            {
                Debug.LogError($"Failed to get channel position in seconds: {Bass.LastError}");
                return -1;
            }

            seconds -= GetDesyncOffset(manager, bufferCompensation);

            return seconds;
        }

        public void SetPosition(BassAudioManager manager, double position, bool bufferCompensation = true)
        {
            position += GetDesyncOffset(manager, bufferCompensation);

            long bytes = Bass.ChannelSeconds2Bytes(StreamHandle, position);
            if (bytes < 0)
            {
                Debug.LogError($"Failed to get byte position at {position}!");
                return;
            }

            bool success = IsMixed
                ? BassMix.ChannelSetPosition(StreamHandle, bytes)
                : Bass.ChannelSetPosition(StreamHandle, bytes);

            if (!success)
            {
                Debug.LogError($"Failed to seek to position {position}!");
                return;
            }
        }

        public double GetLengthInSeconds()
        {
            long length = Bass.ChannelGetLength(StreamHandle);
            if (length < 0)
            {
                Debug.LogError($"Failed to get channel length in bytes: {Bass.LastError}");
                return -1;
            }

            double seconds = Bass.ChannelBytes2Seconds(StreamHandle, length);
            if (seconds < 0)
            {
                Debug.LogError($"Failed to get channel length in seconds: {Bass.LastError}");
                return -1;
            }

            return seconds;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                // Free managed resources here
                if (disposing)
                {
                }

                // Free unmanaged resources here
                _streamHandles.Dispose();
                _reverbHandles.Dispose();

                if (_sourceHandle != 0)
                {
                    if (!Bass.StreamFree(_sourceHandle))
                        Debug.LogError($"Failed to free file stream (THIS WILL LEAK MEMORY!): {Bass.LastError}");
                    _sourceHandle = 0;
                }

                _disposed = true;
            }
        }

        private static bool CreateSourceStream(Stream stream, out int streamHandle)
        {
            // Last flag is new BASS_SAMPLE_NOREORDER flag, which is not in the BassFlags enum,
            // as it was made as part of an update to fix <= 8 channel oggs.
            // https://www.un4seen.com/forum/?topic=20148.msg140872#msg140872
            const BassFlags streamFlags = BassFlags.Prescan | BassFlags.Decode | BassFlags.AsyncFile | (BassFlags) 64;

            streamHandle = Bass.CreateStream(StreamSystem.NoBuffer, streamFlags, new BassStreamProcedures(stream));
            if (streamHandle == 0)
            {
                Debug.LogError($"Failed to create source stream: {Bass.LastError}");
                return false;
            }
            return true;
        }

        private static bool CreateSplitStreams(int sourceStream, double volume, int[] channelMap, out Handles streamHandles, out Handles reverbHandles)
        {
            reverbHandles = default;
            if (!Handles.Create(sourceStream, volume, channelMap, out streamHandles))
            {
                return false;
            }

            if (!Handles.Create(sourceStream, 0, channelMap, out reverbHandles))
            {
                return false;
            }
            return true;
        }

        private static PitchShiftParametersStruct SetPitchParams(AudioOptions options, SongStem stem, float speed, ref Handles streamHandles, ref Handles reverbHandles)
        {
            PitchShiftParametersStruct pitchParams = new(1, 0, AudioOptions.WHAMMY_FFT_DEFAULT, AudioOptions.WHAMMY_OVERSAMPLE_DEFAULT);
            // Set whammy pitch bending if enabled
            if (options.UseWhammyFx && AudioHelpers.PitchBendAllowedStems.Contains(stem))
            {
                // Setting the FFT size causes a crash in BASS_FX :/
                // _pitchParams.FFTSize = _manager.Options.WhammyFFTSize;
                pitchParams.OversampleFactor = options.WhammyOversampleFactor;
                if (SetupPitchBend(pitchParams, ref streamHandles))
                {
                    SetupPitchBend(pitchParams, ref reverbHandles);
                }
            }

            if (!Mathf.Approximately(speed, 1f))
            {
                speed = (float) Math.Round(Math.Clamp(speed, 0.05, 50), 2);
                SetSpeed(speed, streamHandles.Stream, reverbHandles.Stream);
                if (options.IsChipmunkSpeedup)
                {
                    SetChipmunking(speed, streamHandles.Stream, reverbHandles.Stream);
                }
            }
            return pitchParams;
        }

        private static void SetChipmunking(float speed, int streamHandle, int reverbHandle)
        {
            double accurateSemitoneShift = 12 * Math.Log(speed, 2);
            float finalSemitoneShift = (float) Math.Clamp(accurateSemitoneShift, -60, 60);
            if (!Bass.ChannelSetAttribute(streamHandle, ChannelAttribute.Pitch, finalSemitoneShift) ||
                !Bass.ChannelSetAttribute(reverbHandle, ChannelAttribute.Pitch, finalSemitoneShift))
                Debug.LogError($"Failed to set channel pitch: {Bass.LastError}");
        }

        private static void SetSpeed(float speed, int streamHandle, int reverbHandle)
        {
            // Gets relative speed from 100% (so 1.05f = 5% increase)
            float percentageSpeed = speed * 100;
            float relativeSpeed = percentageSpeed - 100;

            if (!Bass.ChannelSetAttribute(streamHandle, ChannelAttribute.Tempo, relativeSpeed) ||
                !Bass.ChannelSetAttribute(reverbHandle, ChannelAttribute.Tempo, relativeSpeed))
            {
                Debug.LogError($"Failed to set channel speed: {Bass.LastError}");
            }
        }

        private static bool SetupPitchBend(in PitchShiftParametersStruct pitchParams, ref Handles handles)
        {
            handles.CompressorFX = BassHelpers.FXAddParameters(handles.Stream, EffectType.PitchShift, pitchParams);
            if (handles.CompressorFX == 0)
            {
                Debug.LogError($"Failed to set up pitch bend for main stream!");
                return false;
            }
            return true;
        }
    }
}