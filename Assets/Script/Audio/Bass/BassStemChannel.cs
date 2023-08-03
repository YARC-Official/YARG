using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using ManagedBass;
using ManagedBass.Fx;
using ManagedBass.Mix;
using UnityEngine;

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
                        Debug.LogWarning($"Failed to free channel stream (THIS WILL LEAK MEMORY!): {Bass.LastError}");
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
                        ? BassMix.ChannelSetSync(_streamHandles.Stream, SyncFlags.End, 0, sync)
                        : Bass.ChannelSetSync(_streamHandles.Stream, SyncFlags.End, 0, sync);
                }

                _channelEnd += value;
            }
            remove { _channelEnd -= value; }
        }

        private readonly string _path;
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

        public BassStemChannel(IAudioManager manager, string path, SongStem stem)
        {
            _manager = manager;
            _path = path;
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
            {
                return -1;
            }

            if (_streamHandles.Stream != 0)
            {
                return 0;
            }

            if (_sourceHandle == 0)
            {
                if (string.IsNullOrEmpty(_path))
                {
                    // Channel was not set up correctly for some reason
                    return -1;
                }

                // Last flag is new BASS_SAMPLE_NOREORDER flag, which is not in the BassFlags enum,
                // as it was made as part of an update to fix <= 8 channel oggs.
                // https://www.un4seen.com/forum/?topic=20148.msg140872#msg140872
                const BassFlags flags = BassFlags.Prescan | BassFlags.Decode | BassFlags.AsyncFile | (BassFlags) 64;

                _sourceHandle = Bass.CreateStream(_path, 0, 0, flags);
                if (_sourceHandle == 0)
                {
                    return (int) Bass.LastError;
                }
            }

            int main = BassMix.CreateSplitStream(_sourceHandle, BassFlags.Decode | BassFlags.SplitPosition, null);
            int reverbSplit =
                BassMix.CreateSplitStream(_sourceHandle, BassFlags.Decode | BassFlags.SplitPosition, null);

            const BassFlags tempoFlags =
                BassFlags.SampleOverrideLowestVolume | BassFlags.Decode | BassFlags.FxFreeSource;

            _streamHandles.Stream = BassFx.TempoCreate(main, tempoFlags);
            _reverbHandles.Stream = BassFx.TempoCreate(reverbSplit, tempoFlags);

            // Apply a compressor to balance stem volume
            Bass.ChannelSetFX(_streamHandles.Stream, EffectType.Compressor, 1);
            Bass.ChannelSetFX(_reverbHandles.Stream, EffectType.Compressor, 1);

            var compressorParams = new CompressorParameters
            {
                fGain = -3,
                fThreshold = -2,
                fAttack = 0.01f,
                fRelease = 0.1f,
                fRatio = 4,
            };

            Bass.FXSetParameters(_streamHandles.Stream, compressorParams);
            Bass.FXSetParameters(_reverbHandles.Stream, compressorParams);

            Bass.ChannelSetAttribute(_streamHandles.Stream, ChannelAttribute.Volume, _manager.GetVolumeSetting(Stem));
            Bass.ChannelSetAttribute(_reverbHandles.Stream, ChannelAttribute.Volume, 0);

            if (_manager.Options.UseWhammyFx && AudioHelpers.PitchBendAllowedStems.Contains(Stem))
            {
                // Setting the FFT size causes a crash in BASS_FX :/
                // _pitchParams.FFTSize = _manager.Options.WhammyFFTSize;
                _pitchParams.OversampleFactor = _manager.Options.WhammyOversampleFactor;

                _streamHandles.PitchFX = Bass.ChannelSetFX(_streamHandles.Stream, EffectType.PitchShift, 0);
                if (_streamHandles.PitchFX == 0)
                {
                    Debug.LogError("Failed to add pitch shift (normal fx): " + Bass.LastError);
                }
                else if (!BassHelpers.FXSetParameters(_streamHandles.PitchFX, _pitchParams))
                {
                    Debug.LogError("Failed to set pitch shift params (normal fx): " + Bass.LastError);
                    Bass.ChannelRemoveFX(_streamHandles.Stream, _streamHandles.PitchFX);
                    _streamHandles.PitchFX = 0;
                }

                _reverbHandles.PitchFX = Bass.ChannelSetFX(_reverbHandles.Stream, EffectType.PitchShift, 0);
                if (_reverbHandles.PitchFX == 0)
                {
                    Debug.LogError("Failed to add pitch shift (reverb fx): " + Bass.LastError);
                }
                else if (!BassHelpers.FXSetParameters(_reverbHandles.PitchFX, _pitchParams))
                {
                    Debug.LogError("Failed to set pitch shift params (reverb fx): " + Bass.LastError);
                    Bass.ChannelRemoveFX(_reverbHandles.Stream, _reverbHandles.PitchFX);
                    _reverbHandles.PitchFX = 0;
                }

                // Set position to trigger the pitch bend delay compensation
                SetPosition(0);
            }

            if (!Mathf.Approximately(speed, 1f))
            {
                SetSpeed(speed);

                // Have to handle pitch separately for some reason
                if (_manager.Options.IsChipmunkSpeedup)
                {
                    float semitoneShift = speed switch
                    {
                        > 1 => speed / 9 - 1 / 9,
                        < 1 => speed / 3 - 1 / 3,
                        _     => 0
                    };

                    semitoneShift = Math.Clamp(semitoneShift, -60, 60);

                    Bass.ChannelSetAttribute(_streamHandles.Stream, ChannelAttribute.Pitch, semitoneShift);
                    Bass.ChannelSetAttribute(_reverbHandles.Stream, ChannelAttribute.Pitch, semitoneShift);
                }
            }

            LengthD = GetLengthInSeconds();

            return 0;
        }

        public void FadeIn(float maxVolume)
        {
            Bass.ChannelSetAttribute(_streamHandles.Stream, ChannelAttribute.Volume, 0);
            Bass.ChannelSlideAttribute(_streamHandles.Stream, ChannelAttribute.Volume, maxVolume,
                BassHelpers.FADE_TIME_MILLISECONDS);
        }

        public UniTask FadeOut()
        {
            Bass.ChannelSlideAttribute(_streamHandles.Stream, ChannelAttribute.Volume, 0, BassHelpers.FADE_TIME_MILLISECONDS);
            return UniTask.WaitUntil(() =>
            {
                Bass.ChannelGetAttribute(_streamHandles.Stream, ChannelAttribute.Volume, out var currentVolume);
                return Mathf.Abs(currentVolume) <= 0.01f;
            });
        }

        public void SetVolume(double newVolume)
        {
            if (_streamHandles.Stream == 0)
            {
                return;
            }

            double volumeSetting = _manager.GetVolumeSetting(Stem);

            double oldBassVol = _lastStemVolume * Volume;
            double newBassVol = volumeSetting * newVolume;

            // Values are the same, no need to change
            if (Math.Abs(oldBassVol - newBassVol) < double.Epsilon)
            {
                return;
            }

            Volume = newVolume;
            _lastStemVolume = volumeSetting;

            Bass.ChannelSetAttribute(_streamHandles.Stream, ChannelAttribute.Volume, newBassVol);

            if (_isReverbing)
            {
                Bass.ChannelSlideAttribute(_reverbHandles.Stream, ChannelAttribute.Volume, (float) (newBassVol * 0.7), 1);
            }
            else
            {
                Bass.ChannelSlideAttribute(_reverbHandles.Stream, ChannelAttribute.Volume, 0, 1);
            }
        }

        public void SetReverb(bool reverb)
        {
            _isReverbing = reverb;
            if (reverb)
            {
                // Reverb already applied
                if (_reverbHandles.ReverbFX != 0) return;

                // Set reverb FX
                _reverbHandles.LowEQ = BassHelpers.AddEqToChannel(_reverbHandles.Stream, BassHelpers.LowEqParams);
                _reverbHandles.MidEQ = BassHelpers.AddEqToChannel(_reverbHandles.Stream, BassHelpers.MidEqParams);
                _reverbHandles.HighEQ = BassHelpers.AddEqToChannel(_reverbHandles.Stream, BassHelpers.HighEqParams);
                _reverbHandles.ReverbFX = BassHelpers.AddReverbToChannel(_reverbHandles.Stream);

                double volumeSetting = _manager.GetVolumeSetting(Stem);
                Bass.ChannelSlideAttribute(_reverbHandles.Stream, ChannelAttribute.Volume,
                    (float) (volumeSetting * Volume * 0.7f), BassHelpers.REVERB_SLIDE_IN_MILLISECONDS);
            }
            else
            {
                // No reverb is applied
                if (_reverbHandles.ReverbFX == 0) return;

                // Remove low-high
                Bass.ChannelRemoveFX(_reverbHandles.Stream, _reverbHandles.LowEQ);
                Bass.ChannelRemoveFX(_reverbHandles.Stream, _reverbHandles.MidEQ);
                Bass.ChannelRemoveFX(_reverbHandles.Stream, _reverbHandles.HighEQ);
                Bass.ChannelRemoveFX(_reverbHandles.Stream, _reverbHandles.ReverbFX);

                _reverbHandles.LowEQ = 0;
                _reverbHandles.MidEQ = 0;
                _reverbHandles.HighEQ = 0;
                _reverbHandles.ReverbFX = 0;

                Bass.ChannelSlideAttribute(_reverbHandles.Stream, ChannelAttribute.Volume, 0,
                    BassHelpers.REVERB_SLIDE_OUT_MILLISECONDS);
            }
        }

        public void SetSpeed(float speed)
        {
            speed = (float)Math.Round(Math.Clamp(speed, 0.05, 50), 2);

            // Gets relative speed from 100% (so 1.05f = 5% increase)
            float percentageSpeed = speed * 100;
            float relativeSpeed = percentageSpeed - 100;

            Bass.ChannelSetAttribute(_streamHandles.Stream, ChannelAttribute.Tempo, relativeSpeed);
            Bass.ChannelSetAttribute(_reverbHandles.Stream, ChannelAttribute.Tempo, relativeSpeed);
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
                double sampleRate = Bass.ChannelGetAttribute(_streamHandles.Stream, ChannelAttribute.Frequency);
                desync += _pitchParams.FFTSize / sampleRate;
            }

            return desync;
        }

        public double GetPosition(bool desyncCompensation = true)
        {
            double position = Bass.ChannelBytes2Seconds(_streamHandles.Stream, Bass.ChannelGetPosition(_streamHandles.Stream));
            if (desyncCompensation)
                position -= GetDesyncOffset();
            return position;
        }

        public void SetPosition(double position, bool desyncCompensation = true)
        {
            if (desyncCompensation)
                position += GetDesyncOffset();

            if (IsMixed)
            {
                BassMix.ChannelSetPosition(_streamHandles.Stream, Bass.ChannelSeconds2Bytes(_streamHandles.Stream, position));
            }
            else
            {
                Bass.ChannelSetPosition(_streamHandles.Stream, Bass.ChannelSeconds2Bytes(_streamHandles.Stream, position));
            }

            if (_sourceIsSplit && !BassMix.SplitStreamReset(_sourceHandle))
                Debug.LogError($"Failed to reset stream: {Bass.LastError}");
        }

        public double GetLengthInSeconds()
        {
            return BassHelpers.GetChannelLengthInSeconds(_streamHandles.Stream);
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
                    Bass.StreamFree(_sourceHandle);
                    _sourceHandle = 0;
                }

                _disposed = true;
            }
        }
    }
}