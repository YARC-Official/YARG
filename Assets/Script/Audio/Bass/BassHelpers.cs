using System;
using ManagedBass;
using ManagedBass.DirectX8;
using ManagedBass.Fx;
using UnityEngine;
using YARG.Core.Logging;

namespace YARG.Audio.BASS
{
    public static class BassHelpers
    {
        public const int PLAYBACK_BUFFER_LENGTH = 75;
        public const double PLAYBACK_BUFFER_DESYNC = PLAYBACK_BUFFER_LENGTH / 1000.0;

        public const float REVERB_VOLUME_MULTIPLIER = 0.85f;

        public const int FADE_TIME_MILLISECONDS = 1000;

        public const int REVERB_SLIDE_IN_MILLISECONDS = 300;
        public const int REVERB_SLIDE_OUT_MILLISECONDS = 500;

        public const EffectType REVERB_TYPE = EffectType.Freeverb;

        public static readonly CompressorParameters CompressorParams = new()
        {
            fGain = -3, fThreshold = -2, fAttack = 0.01f, fRelease = 0.1f, fRatio = 4,
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

        public static readonly DXReverbParameters DXReverbParams = new()
        {
            fInGain = -5f, fReverbMix = 0f, fReverbTime = 1000.0f, fHighFreqRTRatio = 0.001f
        };

        public static readonly ReverbParameters FreeverbParams = new()
        {
            fDryMix = 0.5f, fWetMix = 1.5f, fRoomSize = 0.8f, fDamp = 0.6f, fWidth = 1.0f, lMode = 0
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

        public static unsafe bool FXSetParameters<T>(int Handle, T Parameters)
            where T : unmanaged, IEffectParameter
        {
            return Bass.FXSetParameters(Handle, (IntPtr) (void*) &Parameters);
        }

        public static int AddCompressorToChannel(int handle)
        {
            return FXAddParameters(handle, EffectType.Compressor, CompressorParams);
        }

        public static int AddReverbToChannel(int handle)
        {
            IEffectParameter reverbParams = REVERB_TYPE switch
            {
                EffectType.DXReverb => DXReverbParams,
                EffectType.Freeverb => FreeverbParams,
                _ => throw new ArgumentOutOfRangeException()
            };

            return FXAddParameters(handle, REVERB_TYPE, reverbParams);
        }

        public static int AddEqToChannel(int handle, IEffectParameter eqParams)
        {
            return FXAddParameters(handle, EffectType.PeakEQ, eqParams);
        }

        public static unsafe bool ApplyGain(float gain, IntPtr buffer, int length)
        {
            var sampleBuffer = new Span<float>((void*) buffer, length / sizeof(float));

            foreach (ref float sample in sampleBuffer)
            {
                sample *= gain;
            }

            return true;
        }
    }
}