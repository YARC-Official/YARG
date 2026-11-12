#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
using System.Runtime.InteropServices;
using PlasticBand.Devices;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

namespace YARG.Input.Devices
{
    /// <summary>
    /// The native state of the Mad Catz Rock Band 4 Stratocaster as reported by the xone
    /// <c>xone_gip_madcatz_strat</c> kernel driver through Unity's Linux joystick support.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct XoneMadCatzRB4StratocasterNativeState : IInputStateTypeInfo
    {
        public FourCC format => XoneLinuxState.Format;

        // Buttons in ascending Linux key code order:
        // select, start, guide, frets (green, red, yellow, blue, orange),
        // solo frets (green, red, yellow, blue, orange)
        // See XoneGuitarTranslator for the shared button bits.

#pragma warning disable CS0649
        public ushort buttons;
        public ushort padding; // Required for state alignment, never accessed in code
        public int slider;
        public int whammy;
        public int tilt;
        public int hatX;
        public int hatY;
#pragma warning restore CS0649
    }

    /// <summary>
    /// The Mad Catz Rock Band 4 Stratocaster, when driven by the xone kernel driver on Linux.
    /// </summary>
    [InputControlLayout(stateType = typeof(XoneGuitarState),
        displayName = "Mad Catz Rock Band 4 Stratocaster (xone)")]
    internal class XoneMadCatzRB4Stratocaster : RockBandGuitar, IInputStateCallbackReceiver
    {
        internal const string NativeProduct = "Mad Catz Rock Band 4 Stratocaster";

        internal static void Initialize()
        {
            InputSystem.RegisterLayout<XoneMadCatzRB4Stratocaster>();
            InputSystem.RegisterLayoutMatcher(nameof(XoneMadCatzRB4Stratocaster),
                new InputDeviceMatcher().WithProduct("^" + NativeProduct + "$"));
        }

        private static XoneStateTranslator<XoneMadCatzRB4StratocasterNativeState, XoneGuitarState>
            .TranslateStateHandler s_Translator = TranslateState;

        protected override void FinishSetup()
        {
            base.FinishSetup();

            XoneSdlDescriptor.Verify(description.capabilities, "Mad Catz Rock Band 4 Stratocaster",
                buttonCount: 13,
                (offset: 4, usage: XoneSdlDescriptor.XUsage, hat: false),
                (offset: 8, usage: XoneSdlDescriptor.YUsage, hat: false),
                (offset: 12, usage: XoneSdlDescriptor.ZUsage, hat: false),
                (offset: 16, usage: XoneSdlDescriptor.Hat0XUsage, hat: true),
                (offset: 20, usage: XoneSdlDescriptor.Hat0YUsage, hat: true));
            XoneStateTranslator<XoneMadCatzRB4StratocasterNativeState, XoneGuitarState>.VerifyDevice(this);
        }

        private static XoneGuitarState TranslateState(ref XoneMadCatzRB4StratocasterNativeState state)
            => XoneGuitarTranslator.Translate(state.buttons, state.whammy, state.tilt, state.hatX, state.hatY,
                pickupSwitch: (byte) UnityEngine.Mathf.Clamp(state.slider, 0, 255));

        void IInputStateCallbackReceiver.OnNextUpdate() {}

        void IInputStateCallbackReceiver.OnStateEvent(InputEventPtr eventPtr)
            => XoneStateTranslator<XoneMadCatzRB4StratocasterNativeState, XoneGuitarState>
                .OnStateEvent(this, eventPtr, s_Translator);

        bool IInputStateCallbackReceiver.GetStateOffsetForEvent(InputControl control, InputEventPtr eventPtr,
            ref uint offset)
            => XoneStateTranslator<XoneMadCatzRB4StratocasterNativeState, XoneGuitarState>
                .GetStateOffsetForEvent(this, control, eventPtr, ref offset, s_Translator);
    }
}
#endif
