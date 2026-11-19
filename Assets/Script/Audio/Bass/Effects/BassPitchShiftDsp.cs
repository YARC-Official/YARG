#nullable enable
using System;
using YARG.Audio.BASS.Native;
using YARG.Core.Logging;

namespace YARG.Audio.BASS.Effects
{
    internal sealed class BassPitchShiftDsp : NativeDspHandle
    {
        private const string EFFECT_NAME = "native pitch shift DSP";

        private BassPitchShiftDsp()
        {
        }

        internal uint LatencyFrames { get; private set; }

        internal static BassPitchShiftDsp? Attach(int channelHandle, int priority = 0)
        {
            if (channelHandle == 0)
            {
                YargLogger.LogFormatError("Cannot attach {0}: channel={1}.", EFFECT_NAME, channelHandle);
                return null;
            }

            var dsp = YargAudioNative.Attach(EFFECT_NAME, channelHandle,
                (out BassPitchShiftDsp handle, out int bassError) =>
                    YargAudioBindings.PitchShiftDspAttach(channelHandle, priority, out handle, out bassError));
            if (dsp == null)
            {
                return null;
            }

            if (YargAudioBindings.PitchShiftDspGetLatency(dsp, out uint latency) != 0)
            {
                YargLogger.LogFormatError("Failed to read latency of {0} on channel {1}.", EFFECT_NAME, channelHandle);
                dsp.Dispose();
                return null;
            }

            dsp.LatencyFrames = latency;
            return dsp;
        }

        internal bool SetRatio(float ratio)
        {
            if (!YargAudioNative.IsFinite(ratio) || ratio <= 0)
            {
                YargLogger.LogFormatError("Ignoring invalid ratio for {0}: {1}.", EFFECT_NAME, ratio);
                return false;
            }

            return YargAudioNative.TryInvoke(this, handle => YargAudioBindings.PitchShiftDspSetRatio(handle, ratio));
        }

        internal void RequestReset() =>
            YargAudioNative.TryReset(this, handle => YargAudioBindings.PitchShiftDspReset(handle));

        protected override void Destroy(IntPtr handle) =>
            YargAudioBindings.PitchShiftDspDestroy(handle);
    }
}
