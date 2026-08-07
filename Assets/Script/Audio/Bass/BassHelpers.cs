using System;
using ManagedBass;
using ManagedBass.Fx;
using YARG.Core.Audio;
using YARG.Core.Logging;
using YARG.Settings;

namespace YARG.Audio.BASS
{
    public static class BassHelpers
    {
        public const uint YARG_AUDIO_ABI_VERSION = 1;

        public const int PLAYBACK_BUFFER_LENGTH = 75;
        public const double PLAYBACK_BUFFER_DESYNC = PLAYBACK_BUFFER_LENGTH / 1000.0;

        public const float REVERB_VOLUME_MULTIPLIER = 0.35f;

        public const int FADE_TIME_MILLISECONDS = 1000;

        public static int ConfiguredPlaybackBufferLength => ClampPlaybackBufferLength(
            SettingsManager.Settings?.PlaybackBufferLength.Value ?? 0);

        public const int REVERB_SLIDE_IN_MILLISECONDS = 300;
        public const int REVERB_SLIDE_OUT_MILLISECONDS = 500;

        /*
         * From Bass documentation (http://bass.radio42.com/help/html/4c663bda-2751-c2c3-eaf2-770b846b6652.htm)
         * "With a ratio of 4:1, when the (time averaged) input level is 4 dB over the threshold, the output signal level will be 1 dB over the threshold."
         * "[Additionally,] with any threshold/ratio combination, you could calculate the gain for a 0dB peak like this: fGain=fThreshold*(1/fRatio-1)"
         * 
         * The intention of the gain is to normalize 0dB signals back to 0dB after compression.
         * However, we only want the compressors to handle "clipping" situations (audio that exceeds 0dB).
         * So we set the gain and thresholds both to zero - which still follows the formula.
         * We can then set the ratio to whatever we want.
         * 
         * Note: you don't want to apply a negative gain as the gain value effects ALL audio, not just the part that got compressed.
         * We don't want to make quiet parts even quieter.
         */
        public static readonly CompressorParameters CompressorParams = new()
        {
            fGain = 0f, fThreshold = 0, fAttack = 10f, fRelease = 100f, fRatio = 8,
        };
        
        public static readonly PeakEQParameters LowEqParams = new()
        {
            fBandwidth = 1.25f, fCenter = 250.0f, fGain = -12f
        };

        public static readonly PeakEQParameters MidEqParams = new()
        {
            fBandwidth = 1.25f, fCenter = 2300.0f, fGain = 2.25f
        };

        public static readonly PeakEQParameters HighEqParams = new()
        {
            fBandwidth = 0.75f, fCenter = 6000.0f, fGain = 2.25f
        };

        public static int ClampPlaybackBufferLength(int length)
        {
            int minimumLength = GlobalAudioHandler.MinimumBufferLength;
            if (length > 0 && minimumLength > 0 && length < minimumLength)
            {
                return minimumLength;
            }

            return length;
        }

        public static int FXAddParameters(int streamHandle, EffectType type, IEffectParameter parameters,
            int priority = 0)
        {
            int fxHandle = Bass.ChannelSetFX(streamHandle, type, priority);
            if (fxHandle == 0)
            {
                YargLogger.LogFormatError("Failed to create effects handle for {0}: {1}", type, Bass.LastError);
                return 0;
            }

            if (!Bass.FXSetParameters(fxHandle, parameters))
            {
                YargLogger.LogFormatError("Failed to apply effects parameters for {0}: {1}", type, Bass.LastError);
                Bass.ChannelRemoveFX(streamHandle, fxHandle);
                return 0;
            }

            return fxHandle;
        }

        public static int FXAddParameters<T>(int streamHandle, EffectType type, T parameters, int priority = 0)
            where T : unmanaged, IEffectParameter
        {
            int fxHandle = Bass.ChannelSetFX(streamHandle, type, priority);
            if (fxHandle == 0)
            {
                YargLogger.LogFormatError("Failed to create effects handle: {0}", Bass.LastError);
                return 0;
            }

            if (!FXSetParameters(fxHandle, parameters))
            {
                YargLogger.LogFormatError("Failed to apply effects parameters: {0}", Bass.LastError);
                Bass.ChannelRemoveFX(streamHandle, fxHandle);
                return 0;
            }

            return fxHandle;
        }

        public static unsafe bool FXSetParameters<T>(int Handle, T Parameters)
            where T : unmanaged, IEffectParameter
        {
            return Bass.FXSetParameters(Handle, (IntPtr) (void*) &Parameters);
        }

        public static int AddCompressorToChannel(int handle)
        {
            return FXAddParameters(handle, EffectType.Compressor, CompressorParams);
        }

        public static int AddEqToChannel(int handle, IEffectParameter eqParams)
        {
            return FXAddParameters(handle, EffectType.PeakEQ, eqParams);
        }

        public static int AddPitchShiftToChannel(int handle, IEffectParameter pitchParams)
        {
            return FXAddParameters(handle, EffectType.PitchShift, pitchParams);
        }

        public static int GetOutputChannelCount()
        {
            Bass.GetInfo(out BassInfo info);

            return info.SpeakerCount;
        }

#nullable enable
        public static void UpdateOutputChannels(int stream, OutputChannel? channel)
#nullable disable
        {
            if (channel is not BassOutputChannel bassChannel)
            {
                // Remove assigned output channels
                Bass.ChannelFlags(stream, 0, BassFlags.SpeakerFront);

                return;
            }

            // Set channel(s)
            Bass.ChannelFlags(stream, bassChannel.Flags, BassFlags.SpeakerFront);
        }
    }
}
