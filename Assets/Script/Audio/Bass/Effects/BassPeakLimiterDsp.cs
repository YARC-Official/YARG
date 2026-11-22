#nullable enable
using System;
using YARG.Audio.BASS.Native;
using YARG.Core.Logging;

namespace YARG.Audio.BASS.Effects
{
    /// <summary>
    /// Owns a zero-latency peak limiter implemented and attached entirely by the YargAudio native plugin.
    /// The BASS channel passed to <see cref="Attach"/> must outlive this handle.
    /// </summary>
    internal sealed class BassPeakLimiterDsp : NativeDspHandle
    {
        private const string EFFECT_NAME = "native Peak Limiter DSP";

        private BassPeakLimiterDsp()
        {
        }

        internal static BassPeakLimiterDsp? Attach(int channelHandle, float ceiling, float releaseMs, int priority)
        {
            if (channelHandle == 0 || !YargAudioNative.IsFinite(ceiling) || ceiling <= 0 ||
                !YargAudioNative.IsFinite(releaseMs) || releaseMs <= 0)
            {
                YargLogger.LogFormatError(
                    "Cannot attach {0}: channel={1}, ceiling={2}, release={3}, priority={4}.",
                    EFFECT_NAME, channelHandle, ceiling, releaseMs, priority);
                return null;
            }

            return YargAudioNative.Attach(EFFECT_NAME, channelHandle,
                (out BassPeakLimiterDsp handle, out int bassError) =>
                    YargAudioBindings.PeakLimiterDspAttach(channelHandle, ceiling, releaseMs, priority, out handle,
                        out bassError));
        }

        protected override void Destroy(IntPtr handle) =>
            YargAudioBindings.PeakLimiterDspDestroy(handle);
    }
}
