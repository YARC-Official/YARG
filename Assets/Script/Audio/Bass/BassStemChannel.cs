using ManagedBass;
using ManagedBass.Mix;
using UnityEngine;
using YARG.Core.Audio;
using YARG.Core.Logging;

namespace YARG.Audio.BASS
{
    public sealed class BassStemChannel : StemChannel
    {
        private readonly int _sourceHandle;

        private StreamHandle _streamHandles;
        private StreamHandle _reverbHandles;
        private PitchShiftParametersStruct _pitchParams;

        private double _volume;
        private bool _isReverbing;

        internal BassStemChannel(AudioManager manager, SongStem stem, bool clampStemVolume, int sourceStream, in PitchShiftParametersStruct pitchParams, in StreamHandle streamHandles, in StreamHandle reverbHandles)
            : base(manager, stem, clampStemVolume)
        {
            _sourceHandle = sourceStream;
            _streamHandles = streamHandles;
            _reverbHandles = reverbHandles;
            _pitchParams = pitchParams;

            double volume = GlobalAudioHandler.GetTrueVolume(stem);
            if (clampStemVolume && volume < MINIMUM_STEM_VOLUME)
            {
                volume = MINIMUM_STEM_VOLUME;
            }
            SetVolume_Internal(volume);
        }

        protected override void SetWhammyPitch_Internal(float percent)
        {
            // If pitch effect hasn't been added yet, add it.
            if (_streamHandles.PitchFX == 0)
            {
                _streamHandles.PitchFX = BassHelpers.AddPitchShiftToChannel(_streamHandles.Stream, _pitchParams);
            }
            if (_reverbHandles.PitchFX == 0)
            {
                _reverbHandles.PitchFX = BassHelpers.AddPitchShiftToChannel(_reverbHandles.Stream, _pitchParams);
            }

            // Calculate shift
            float shift = Mathf.Pow(2, -(GlobalAudioHandler.WhammyPitchShiftAmount * percent) / 12);
            _pitchParams.fPitchShift = shift;

            // If we have pitch effect, pitch
            if (_streamHandles.PitchFX != 0)
            {
                if (!BassHelpers.FXSetParameters(_streamHandles.PitchFX, _pitchParams))
                {
                    YargLogger.LogFormatError("Failed to set pitch on stream: {0}", Bass.LastError);
                }
            }
            if (_reverbHandles.PitchFX != 0)
            {
                if (!BassHelpers.FXSetParameters(_reverbHandles.PitchFX, _pitchParams))
                {
                    YargLogger.LogFormatError("Failed to set pitch on reverb: {0}", Bass.LastError);
                }
            }
            /*
            else
            {
                // If pitch is effect running we could remove it.
                // This would help with delay but there's a skip when adding or removing.
                // Probably better to do this after at zero whammy rest for a period of time.

                if (_streamHandles.PitchFX != 0)
                {
                    if (!Bass.ChannelRemoveFX(_streamHandles.Stream, _streamHandles.PitchFX))
                    {
                        YargLogger.LogFormatError("Failed to remove pitch effect: {0}!", Bass.LastError);
                    }
                    if (!Bass.ChannelRemoveFX(_reverbHandles.Stream, _streamHandles.PitchFX))
                    {
                        YargLogger.LogFormatError("Failed to remove pitch effect: {0}!", Bass.LastError);
                    }
                    _streamHandles.PitchFX = 0;
                    _reverbHandles.PitchFX = 0;
                }
            }
            */
        }

        protected override void SetPosition_Internal(double position)
        {
            if (_sourceHandle != 0)
            {
                BassMix.SplitStreamReset(_sourceHandle);
            }

            long bytes = Bass.ChannelSeconds2Bytes(_streamHandles.Stream, position);
            if (bytes < 0)
            {
                YargLogger.LogFormatError("Failed to get byte position at {0}!", position);
                return;
            }

            bool success = BassMix.ChannelSetPosition(_streamHandles.Stream, bytes, PositionFlags.Bytes | PositionFlags.MixerReset);
            if (!success)
            {
                YargLogger.LogFormatError("Failed to seek to position {0}!", position);
            }
        }

        protected override void SetSpeed_Internal(float speed, bool shiftPitch)
        {
            BassAudioManager.SetSpeed(speed, _streamHandles.Stream, _reverbHandles.Stream, shiftPitch);
        }

        protected override void SetVolume_Internal(double volume)
        {
            _volume = volume;

            // Using ChannelSlideAttribute with a duration of 0 here instead of ChannelSetAttribute
            // This will cancel any slides in progress that were started SetReverb_Internal
            if (!Bass.ChannelSlideAttribute(_streamHandles.Stream, ChannelAttribute.Volume, (float) volume, 0))
                YargLogger.LogFormatError("Failed to set stream volume: {0}!", Bass.LastError);

            float reverbVolume = _isReverbing ? (float) volume * BassHelpers.REVERB_VOLUME_MULTIPLIER : 0;
            
            if (!Bass.ChannelSlideAttribute(_reverbHandles.Stream, ChannelAttribute.Volume, reverbVolume, 0))
                YargLogger.LogFormatError("Failed to set reverb volume: {0}!", Bass.LastError);
        }

        protected override void SetReverb_Internal(bool reverb)
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

                float volume = (float) (_volume * BassHelpers.REVERB_VOLUME_MULTIPLIER);
                if (!Bass.ChannelSlideAttribute(_reverbHandles.Stream, ChannelAttribute.Volume, volume, BassHelpers.REVERB_SLIDE_IN_MILLISECONDS))
                {
                    YargLogger.LogFormatError("Failed to set reverb volume: {0}!", Bass.LastError);
                }

                if (!Bass.ChannelSlideAttribute(_streamHandles.Stream, ChannelAttribute.Volume, volume, BassHelpers.REVERB_SLIDE_IN_MILLISECONDS))
                {
                    YargLogger.LogFormatError("Failed to set reverb volume: {0}!", Bass.LastError);
                }
            }
            else
            {
                // No reverb is applied
                if (_reverbHandles.ReverbFX == 0) return;

                // Remove low-high
                if (!Bass.ChannelRemoveFX(_reverbHandles.Stream, _reverbHandles.LowEQ) ||
                    !Bass.ChannelRemoveFX(_reverbHandles.Stream, _reverbHandles.MidEQ) ||
                    !Bass.ChannelRemoveFX(_reverbHandles.Stream, _reverbHandles.HighEQ) ||
                    !Bass.ChannelRemoveFX(_reverbHandles.Stream, _reverbHandles.ReverbFX))
                {
                    YargLogger.LogFormatError("Failed to remove effects: {0}!", Bass.LastError);
                }

                _reverbHandles.LowEQ = 0;
                _reverbHandles.MidEQ = 0;
                _reverbHandles.HighEQ = 0;
                _reverbHandles.ReverbFX = 0;

                if (!Bass.ChannelSlideAttribute(_reverbHandles.Stream, ChannelAttribute.Volume, 0, BassHelpers.REVERB_SLIDE_OUT_MILLISECONDS))
                {
                    YargLogger.LogFormatError("Failed to set reverb volume: {0}!", Bass.LastError);
                }

                if (!Bass.ChannelSlideAttribute(_streamHandles.Stream, ChannelAttribute.Volume, (float)_volume, BassHelpers.REVERB_SLIDE_OUT_MILLISECONDS))
                {
                    YargLogger.LogFormatError("Failed to set reverb volume: {0}!", Bass.LastError);
                }
            }
        }

        protected override void DisposeUnmanagedResources()
        {
            _streamHandles.Dispose();
            _reverbHandles.Dispose();

            if (_sourceHandle != 0)
            {
                if (!Bass.StreamFree(_sourceHandle) && Bass.LastError != Errors.Handle)
                    YargLogger.LogFormatError("Failed to free file stream (THIS WILL LEAK MEMORY): {0}!", Bass.LastError);
            }
        }
    }
}