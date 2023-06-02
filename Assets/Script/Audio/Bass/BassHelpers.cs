using System;
using ManagedBass;
using ManagedBass.DirectX8;
using ManagedBass.Fx;

namespace YARG.Audio.BASS {
	public static class BassHelpers {

		public const float SONG_VOLUME_MULTIPLIER = 0.7f;

		public const int FADE_TIME_MILLISECONDS = 1000;

		public const int REVERB_SLIDE_IN_MILLISECONDS = 300;
		public const int REVERB_SLIDE_OUT_MILLISECONDS = 500;

		public const EffectType REVERB_TYPE = EffectType.Freeverb;

		public static readonly PeakEQParameters LowEqParams = new() {
			fBandwidth = 1.25f,
			fCenter = 250.0f,
			fGain = -12f
		};

		public static readonly PeakEQParameters MidEqParams = new() {
			fBandwidth = 1.25f,
			fCenter = 2300.0f,
			fGain = 2.25f
		};

		public static readonly PeakEQParameters HighEqParams = new() {
			fBandwidth = 0.75f,
			fCenter = 6000.0f,
			fGain = 2f
		};

		public static int AddReverbToChannel(int handle) {
			// Set reverb FX
			int reverbFxHandle = Bass.ChannelSetFX(handle, REVERB_TYPE, 0);
			if (reverbFxHandle == 0) {
				return 0;
			}

			IEffectParameter reverbParams = REVERB_TYPE switch {
				EffectType.DXReverb => new DXReverbParameters {
					fInGain = -5f, fReverbMix = 0f, fReverbTime = 1000.0f, fHighFreqRTRatio = 0.001f
				},
				EffectType.Freeverb => new ReverbParameters() {
					fDryMix = 0.5f,
					fWetMix = 1.5f,
					fRoomSize = 0.75f,
					fDamp = 0.5f,
					fWidth = 1.0f,
					lMode = 0
				},
				_ => throw new ArgumentOutOfRangeException()
			};

			return !Bass.FXSetParameters(reverbFxHandle, reverbParams) ? 0 : reverbFxHandle;
		}

		public static int AddEqToChannel(int handle, IEffectParameter eqParams) {
			int eqHandle = Bass.ChannelSetFX(handle, EffectType.PeakEQ, 0);
			if (eqHandle == 0) {
				return 0;
			}

			return !Bass.FXSetParameters(eqHandle, eqParams) ? 0 : eqHandle;
		}

		public static double GetChannelLengthInSeconds(int channel) {
			long length = Bass.ChannelGetLength(channel);

			if (length == -1) {
				return (double) Bass.LastError;
			}

			double seconds = Bass.ChannelBytes2Seconds(channel, length);

			if (seconds < 0) {
				return (double) Bass.LastError;
			}

			return seconds;
		}

		public static unsafe bool ApplyGain(float gain, IntPtr buffer, int length) {
			var bufferPtr = (float*) buffer;

			for (int i = 0; i < length / sizeof(float); i++) {
				bufferPtr![i] *= gain;
			}

			return true;
		}

	}
}