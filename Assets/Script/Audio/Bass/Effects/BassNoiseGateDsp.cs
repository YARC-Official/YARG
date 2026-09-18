#nullable enable
using System;
using System.Runtime.InteropServices;
using YARG.Audio.BASS.Native;
using YARG.Core.Logging;

namespace YARG.Audio.BASS.Effects
{
    internal sealed class BassNoiseGateDsp : NativeDspHandle
    {
        private const string EFFECT_NAME = "native noise gate DSP";

        private BassNoiseGateDsp()
        {
        }

        internal static BassNoiseGateDsp? Attach(int channelHandle, float threshold,
            float floorGain, float attackMs, float holdMs, float releaseMs, int priority = 0)
        {
            if (channelHandle == 0 || !YargAudioNative.AreFinite(threshold, floorGain, attackMs, holdMs, releaseMs))
            {
                YargLogger.LogFormatError(
                    "Cannot attach {0}: channel={1}, threshold={2}, floor={3}, attack={4}, hold={5}, release={6}, priority={7}.",
                    EFFECT_NAME, channelHandle, threshold, floorGain, attackMs, holdMs, releaseMs, priority);
                return null;
            }

            return YargAudioNative.Attach(EFFECT_NAME, channelHandle,
                (out BassNoiseGateDsp handle, out int bassError) =>
                    YargAudioBindings.NoiseGateDspAttach(unchecked((uint) channelHandle), threshold, floorGain,
                        attackMs, holdMs, releaseMs, priority, out handle, out bassError));
        }

        internal bool Reset() =>
            YargAudioNative.TryInvoke(this, handle => YargAudioBindings.NoiseGateDspReset(handle));

        [StructLayout(LayoutKind.Sequential)]
        public struct NoiseGateParams
        {
            public uint Size;
            public float Threshold;
            public float FloorGain;
            public float AttackMs;
            public float HoldMs;
            public float ReleaseMs;

            public NoiseGateParams(float threshold, float floorGain, float attackMs, float holdMs, float releaseMs)
            {
                Size = (uint) Marshal.SizeOf<NoiseGateParams>();
                Threshold = threshold;
                FloorGain = floorGain;
                AttackMs = attackMs;
                HoldMs = holdMs;
                ReleaseMs = releaseMs;
            }
        }

        internal bool SetParams(float threshold, float floorGain, float attackMs, float holdMs, float releaseMs) =>
            SetParams(new NoiseGateParams(threshold, floorGain, attackMs, holdMs, releaseMs));

        internal bool SetParams(in NoiseGateParams parms)
        {
            if (!YargAudioNative.AreFinite(parms.Threshold, parms.FloorGain, parms.AttackMs, parms.HoldMs, parms.ReleaseMs))
            {
                YargLogger.LogFormatError("Ignoring non-finite NoiseGate params for {0}: threshold={1}, floor={2}, attack={3}, hold={4}, release={5}.", EFFECT_NAME, parms.Threshold, parms.FloorGain, parms.AttackMs, parms.HoldMs, parms.ReleaseMs);
                return false;
            }

            var parameters = parms;
            return YargAudioNative.TryInvoke(this, handle => YargAudioBindings.NoiseGateDspSetParams(handle, in parameters));
        }

        protected override void Destroy(IntPtr handle) =>
            YargAudioBindings.NoiseGateDspDestroy(handle);
    }
}
