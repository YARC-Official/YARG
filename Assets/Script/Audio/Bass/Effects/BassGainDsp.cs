#nullable enable
using System;
using System.Runtime.InteropServices;
using YARG.Audio.BASS.Native;
using YARG.Core.Logging;

namespace YARG.Audio.BASS.Effects
{
    /// <summary>
    /// Owns a Gain DSP implemented and attached entirely by the YargAudio native plugin.
    /// The BASS channel passed to <see cref="Attach"/> must outlive this handle.
    /// </summary>
    internal sealed class BassGainDsp : NativeDspHandle
    {
        private const string EFFECT_NAME = "native Gain DSP";

        private BassGainDsp() : base()
        {
        }

        internal static BassGainDsp? Attach(int channelHandle, float initialGain, int priority = 0)
        {
            if (channelHandle == 0 || !YargAudioNative.IsFinite(initialGain))
            {
                YargLogger.LogFormatError(
                    "Cannot attach {0}: channel={1}, gain={2}, priority={3}.",
                    EFFECT_NAME, channelHandle, initialGain, priority);
                return null;
            }

            return YargAudioNative.Attach(EFFECT_NAME, channelHandle,
                (out BassGainDsp handle, out int bassError) =>
                    Native.Attach(unchecked((uint) channelHandle), initialGain, priority, out handle, out bassError));
        }

        internal bool SetGain(float gain)
        {
            if (!YargAudioNative.IsFinite(gain))
            {
                YargLogger.LogFormatError("Ignoring non-finite gain for {0}: {1}.",
                    EFFECT_NAME, gain);
                return false;
            }

            return YargAudioNative.TryInvoke(this, handle => Native.SetGain(handle, gain));
        }

        protected override void Destroy(IntPtr handle)
        {
            Native.Destroy(handle);
        }

        private static class Native
        {
            private const string LIBRARY = "yarg_audio";

            [DllImport(LIBRARY, EntryPoint = "yarg_gain_dsp_attach",
                CallingConvention = CallingConvention.Cdecl)]
            internal static extern int Attach(uint channel, float gain, int priority,
                out BassGainDsp dsp, out int bassError);

            [DllImport(LIBRARY, EntryPoint = "yarg_gain_dsp_set_gain",
                CallingConvention = CallingConvention.Cdecl)]
            internal static extern int SetGain(BassGainDsp dsp, float gain);

            [DllImport(LIBRARY, EntryPoint = "yarg_gain_dsp_destroy",
                CallingConvention = CallingConvention.Cdecl)]
            internal static extern void Destroy(IntPtr dsp);
        }
    }
}
