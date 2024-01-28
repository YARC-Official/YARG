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
    public class BassStemChannel : IStemChannel
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

        public SongStem Stem { get; }
        public double LengthD { get; private set; }

        public double Volume { get; private set; }

        public int StreamHandle => _streamHandles.Stream;
        public int ReverbStreamHandle => _reverbHandles.Stream;

        public bool IsMixed { get; set; } = false;

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

        private readonly Stream _stream;
        private readonly IAudioManager _manager;

        private double _lastStemVolume;

        private int _sourceHandle;
        private bool _sourceIsSplit;

        private Handles _streamHandles;
        private Handles _reverbHandles;

        private bool _isReverbing;
        private bool _disposed;

		private PitchShiftParametersStruct _pitchParams = new(1, 0, AudioOptions.WHAMMY_FFT_DEFAULT,
            AudioOptions.WHAMMY_OVERSAMPLE_DEFAULT);

        public BassStemChannel(IAudioManager manager, Stream stream, SongStem stem)
        {
            _manager = manager;
            _stream = stream;
            Stem = stem;

            Volume = 1;

            _lastStemVolume = _manager.GetVolumeSetting(Stem);
        }

        public BassStemChannel(IAudioManager manager, SongStem stem, int sourceStream, bool isSplit)
        {
            _manager = manager;
            _sourceHandle = sourceStream;
            _sourceIsSplit = isSplit;

            Stem = stem;
            Volume = 1;

            _lastStemVolume = _manager.GetVolumeSetting(Stem);
        }

        ~BassStemChannel()
        {
            Dispose(false);
        }

        public int Load(float speed)
        {
            if (_disposed)
                return (int) Errors.Handle;

            if (StreamHandle != 0)
                return (int) Errors.Already;

            if (!CreateStreams())
                return (int) Errors.Create;

            // Set starting volume
            if (!Bass.ChannelSetAttribute(StreamHandle, ChannelAttribute.Volume, _manager.GetVolumeSetting(Stem)) ||
                !Bass.ChannelSetAttribute(ReverbStreamHandle, ChannelAttribute.Volume, 0))
                Debug.LogError($"Failed to set channel volume: {Bass.LastError}");

            SetEffects();

            // Apply song speed
            if (!Mathf.Approximately(speed, 1f))
            {
                SetSpeed(speed);

                // Have to handle pitch separately for some reason
                if (_manager.Options.IsChipmunkSpeedup)
                {
                    double accurateSemitoneShift = 12 * Math.Log(speed, 2);
                    float finalSemitoneShift = (float) Math.Clamp(accurateSemitoneShift, -60, 60);
                    if (!Bass.ChannelSetAttribute(StreamHandle, ChannelAttribute.Pitch, finalSemitoneShift) ||
                        !Bass.ChannelSetAttribute(ReverbStreamHandle, ChannelAttribute.Pitch, finalSemitoneShift))
                        Debug.LogError($"Failed to set channel pitch: {Bass.LastError}");
                }
            }

            LengthD = GetLengthInSeconds();

            // Set position to trigger delay compensation
            SetPosition(0);

            return 0;
        }

        private bool CreateStreams()
        {
            // Last flag is new BASS_SAMPLE_NOREORDER flag, which is not in the BassFlags enum,
            // as it was made as part of an update to fix <= 8 channel oggs.
            // https://www.un4seen.com/forum/?topic=20148.msg140872#msg140872
            const BassFlags streamFlags = BassFlags.Prescan | BassFlags.Decode | BassFlags.AsyncFile | (BassFlags) 64;
            const BassFlags splitFlags = BassFlags.Decode | BassFlags.SplitPosition;
            const BassFlags tempoFlags = BassFlags.SampleOverrideLowestVolume | BassFlags.Decode |
                BassFlags.FxFreeSource;

            if (_sourceHandle == 0)
            {
                if (_stream == null)
                    // Channel was not set up correctly for some reason
                    return false;

                _sourceHandle = Bass.CreateStream(StreamSystem.NoBuffer, streamFlags, new BassMoggProcedures(_stream, 0));
                if (_sourceHandle == 0)
                {
                    Debug.LogError($"Failed to create file stream: {Bass.LastError}");
                    return false;
                }
            }

            int streamSplit = BassMix.CreateSplitStream(_sourceHandle, splitFlags, null);
            if (streamSplit == 0)
            {
                Debug.LogError($"Failed to create main stream: {Bass.LastError}");
                return false;
            }

            int reverbSplit = BassMix.CreateSplitStream(_sourceHandle, splitFlags, null);
            if (reverbSplit == 0)
            {
                Debug.LogError($"Failed to create reverb stream: {Bass.LastError}");
                return false;
            }

            _streamHandles.Stream = BassFx.TempoCreate(streamSplit, tempoFlags);
            _reverbHandles.Stream = BassFx.TempoCreate(reverbSplit, tempoFlags);

            return true;
        }

        private void SetEffects()
        {
            // Apply a compressor to balance stem volume
            SetCompressor();

            // Set whammy pitch bending if enabled
            if (_manager.Options.UseWhammyFx && AudioHelpers.PitchBendAllowedStems.Contains(Stem))
            {
                SetPitchBend();
            }
        }

        private bool SetCompressor()
        {
            int streamCompressor = BassHelpers.AddCompressorToChannel(StreamHandle);
            if (streamCompressor == 0)
            {
                Debug.LogError($"Failed to set up compressor for main stream!");
                return false;
            }

            int reverbCompressor = BassHelpers.AddCompressorToChannel(ReverbStreamHandle);
            if (reverbCompressor == 0)
            {
                Debug.LogError($"Failed to set up compressor for reverb stream!");
                return false;
            }

            _streamHandles.CompressorFX = streamCompressor;
            _reverbHandles.CompressorFX = reverbCompressor;

            return true;
        }

        private bool SetPitchBend()
        {
            // Setting the FFT size causes a crash in BASS_FX :/
            // _pitchParams.FFTSize = _manager.Options.WhammyFFTSize;
            _pitchParams.OversampleFactor = _manager.Options.WhammyOversampleFactor;

            int streamPitch = BassHelpers.FXAddParameters(StreamHandle, EffectType.PitchShift, _pitchParams);
            if (streamPitch == 0)
            {
                Debug.LogError($"Failed to set up pitch bend for main stream!");
                return false;
            }

            int reverbPitch = BassHelpers.FXAddParameters(ReverbStreamHandle, EffectType.PitchShift, _pitchParams);
            if (reverbPitch == 0)
            {
                Debug.LogError($"Failed to set up pitch bend for reverb stream!");
                return false;
            }

            _streamHandles.CompressorFX = streamPitch;
            _reverbHandles.CompressorFX = reverbPitch;

            return true;
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

        public void SetVolume(double newVolume)
        {
            if (StreamHandle == 0)
            {
                return;
            }

            double volumeSetting = _manager.GetVolumeSetting(Stem);

            double oldBassVol = _lastStemVolume * Volume;
            double newBassVol = volumeSetting * newVolume;

            // Limit minimum stem volume
            if (_manager.Options.UseMinimumStemVolume)
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

        public void SetReverb(bool reverb)
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

                double volumeSetting = _manager.GetVolumeSetting(Stem);
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

            // Gets relative speed from 100% (so 1.05f = 5% increase)
            float percentageSpeed = speed * 100;
            float relativeSpeed = percentageSpeed - 100;

            if (!Bass.ChannelSetAttribute(StreamHandle, ChannelAttribute.Tempo, relativeSpeed) ||
                !Bass.ChannelSetAttribute(ReverbStreamHandle, ChannelAttribute.Tempo, relativeSpeed))
            {
                Debug.LogError($"Failed to set channel speed: {Bass.LastError}");
            }
        }

        public void SetWhammyPitch(float percent)
        {
            if (_streamHandles.PitchFX == 0 || _reverbHandles.PitchFX == 0)
                return;

            percent = Mathf.Clamp(percent, 0f, 1f);

            float shift = Mathf.Pow(2, -(_manager.Options.WhammyPitchShiftAmount * percent) / 12);
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

        private double GetDesyncOffset()
        {
            double desync = BassHelpers.PLAYBACK_BUFFER_DESYNC;

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

        public double GetPosition(bool desyncCompensation = true)
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

            if (desyncCompensation)
                seconds -= GetDesyncOffset();

            return seconds;
        }

        public void SetPosition(double position, bool desyncCompensation = true)
        {
            if (desyncCompensation)
                position += GetDesyncOffset();

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

            if (_sourceIsSplit && !BassMix.SplitStreamReset(_sourceHandle))
                Debug.LogError($"Failed to reset stream: {Bass.LastError}");
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
    }
}