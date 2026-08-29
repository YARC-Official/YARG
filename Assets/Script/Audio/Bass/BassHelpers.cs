using System;
using System.Collections.Generic;
using ManagedBass;
using ManagedBass.Fx;
using YARG.Audio.BASS.Effects;
using YARG.Core.Audio;
using YARG.Core.Logging;
using YARG.Settings;

namespace YARG.Audio.BASS
{
    internal sealed class StreamHandle
    {
        public readonly int Stream;

        internal StreamHandle(int stream)
        {
            Stream = stream;
        }

        internal void Free()
        {
            if (!Bass.StreamFree(Stream) && Bass.LastError != Errors.Handle)
            {
                YargLogger.LogFormatError("Failed to free channel stream (THIS WILL LEAK MEMORY): {0}!",
                    Bass.LastError);
            }
        }

#pragma warning disable CS0649
        public int CompressorFX;
        public int PitchFX;
        public int LowEQ;
        public int MidEQ;
        public int HighEQ;
#pragma warning restore CS0649
    }

    public static class BassHelpers
    {
        public const uint YARG_AUDIO_ABI_VERSION = 8;

        /// <summary>
        /// Floor applied to the playback speed before it is used as a divisor or to scale a
        /// duration, so that a zero or near-zero speed cannot produce an infinite result.
        /// </summary>
        public const float MINIMUM_SPEED = 0.0001f;

        public const float REVERB_VOLUME_MULTIPLIER = 0.35f;

        public const int FADE_TIME_MILLISECONDS = 1000;

        public const int REVERB_SLIDE_IN_MILLISECONDS  = 300;
        public const int REVERB_SLIDE_OUT_MILLISECONDS = 500;

        private const double BASE   = 2;
        private const double FACTOR = BASE - 1;

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
            fGain = 0f,
            fThreshold = 0,
            fAttack = 10f,
            fRelease = 100f,
            fRatio = 8,
        };

        public static readonly PeakEQParameters LowEqParams = new()
        {
            fBandwidth = 1.25f,
            fCenter = 250.0f,
            fGain = -12f,
        };

        public static readonly PeakEQParameters MidEqParams = new()
        {
            fBandwidth = 1.25f,
            fCenter = 2300.0f,
            fGain = 2.25f,
        };

        public static readonly PeakEQParameters HighEqParams = new()
        {
            fBandwidth = 0.75f,
            fCenter = 6000.0f,
            fGain = 2.25f,
        };

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

        public static unsafe bool FXSetParameters<T>(int Handle, T Parameters) where T : unmanaged, IEffectParameter =>
            Bass.FXSetParameters(Handle, (IntPtr) (&Parameters));

        public static int AddCompressorToChannel(int handle) =>
            FXAddParameters(handle, EffectType.Compressor, CompressorParams);

        public static int AddEqToChannel(int handle, IEffectParameter eqParams) =>
            FXAddParameters(handle, EffectType.PeakEQ, eqParams);

        public static int AddPitchShiftToChannel(int handle, IEffectParameter pitchParams) =>
            FXAddParameters(handle, EffectType.PitchShift, pitchParams);

#nullable enable
        public static IBassReverbDsp? CreateReverb(ReverbMode mode, int streamHandle, float dryMix, float wetMix,
            float roomSize, float damp, float width = 1f, int priority = 0)
        {
            return mode switch
            {
                ReverbMode.Quality => BassDattorroReverbDsp.Create(streamHandle, dryMix, wetMix, roomSize, damp,
                    width, priority),
                _ => BassFreeverbDsp.Create(streamHandle, dryMix, wetMix, roomSize, damp, width, priority)
            };
        }
#nullable disable

        public static int GetOutputChannelCount()
        {
            Bass.GetInfo(out var info);

            return info.SpeakerCount;
        }

        internal static PitchShiftParametersStruct SetPitchParams(SongStem stem, StreamHandle streamHandles,
            StreamHandle reverbHandles)
        {
            PitchShiftParametersStruct pitchParams = new(1, 0, GlobalAudioHandler.WHAMMY_FFT_DEFAULT,
                GlobalAudioHandler.WHAMMY_OVERSAMPLE_DEFAULT);
            if (GlobalAudioHandler.UseWhammyFx && AudioHelpers.PitchBendAllowedStems.Contains(stem))
            {
                pitchParams.OversampleFactor = GlobalAudioHandler.WhammyOversampleFactor;
                if (SetupPitchBend(pitchParams, streamHandles))
                {
                    SetupPitchBend(pitchParams, reverbHandles);
                }
            }

            return pitchParams;
        }

        internal static bool SetupPitchBend(in PitchShiftParametersStruct pitchParams, StreamHandle handles)
        {
            handles.PitchFX = FXAddParameters(handles.Stream, EffectType.PitchShift, pitchParams);
            if (handles.PitchFX == 0)
            {
                YargLogger.LogError("Failed to set up pitch bend for main stream!");
                return false;
            }

            return true;
        }

        internal static double GetLengthInSeconds(int handle)
        {
            long length = Bass.ChannelGetLength(handle);
            if (length < 0)
            {
                YargLogger.LogFormatError("Failed to get channel length in bytes: {0}!", Bass.LastError);
                return -1;
            }

            double seconds = Bass.ChannelBytes2Seconds(handle, length);
            if (seconds < 0)
            {
                YargLogger.LogFormatError("Failed to get channel length in seconds: {0}!", Bass.LastError);
                return -1;
            }

            return seconds;
        }

#nullable enable
        internal static float[,]? BuildVolumeMatrix(StemMixer.StemInfo info)
        {
            if (info.Indices == null || info.Panning == null)
            {
                return null;
            }

            return BuildVolumeMatrix(new[]
            {
                info,
            }, info.Indices.Length);
        }

        internal static float[,]? BuildVolumeMatrix(IEnumerable<StemMixer.StemInfo> infos, int totalChannels)
        {
            if (totalChannels == 0)
            {
                return null;
            }

            float[,] matrix = new float[2, totalChannels];
            int channelIndex = 0;

            foreach (var info in infos)
            {
                float[] panning = info.Panning!;
                for (int i = 0; i < info.Indices!.Length; i++)
                {
                    matrix[0, channelIndex] = panning[2 * i];
                    matrix[1, channelIndex] = panning[2 * i + 1];
                    channelIndex++;
                }
            }

            return matrix;
        }

        internal static double ExponentialVolume(double volume) => (Math.Pow(BASE, volume) - 1) / FACTOR;

        internal static double LogarithmicVolume(double volume) => Math.Log(FACTOR * volume + 1, BASE);
    }
}
