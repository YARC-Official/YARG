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
    /// The native state of the PDP Rock Band 4 Jaguar as reported by the xone
    /// <c>xone_gip_pdp_jaguar</c> kernel driver through Unity's Linux joystick support.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct XonePdpRB4JaguarNativeState : IInputStateTypeInfo
    {
        public FourCC format => XoneLinuxState.Format;

        // Buttons in ascending Linux key code order:
        // select, start, guide, frets (green, red, yellow, blue, orange),
        // solo frets (green, red, yellow, blue, orange)
        // See XoneGuitarTranslator for the shared button bits.

#pragma warning disable CS0649
        public ushort buttons;
        public ushort padding; // Required for state alignment, never accessed in code
        public int whammy;
        public int tilt;
        public int hatX;
        public int hatY;
#pragma warning restore CS0649
    }

    /// <summary>
    /// The PDP Rock Band 4 Jaguar (Xbox One Riffmaster), when driven by the xone
    /// kernel driver on Linux.
    /// </summary>
    [InputControlLayout(stateType = typeof(XoneGuitarState),
        displayName = "PDP Rock Band 4 Jaguar (xone)")]
    internal class XonePdpRB4Jaguar : RockBandGuitar, IInputStateCallbackReceiver
    {
        internal const string NativeProduct = "PDP Rock Band 4 Jaguar";

        internal static void Initialize()
        {
            InputSystem.RegisterLayout<XonePdpRB4Jaguar>();
            InputSystem.RegisterLayoutMatcher(nameof(XonePdpRB4Jaguar),
                new InputDeviceMatcher().WithProduct("^" + NativeProduct + "$"));
        }

        private static XoneStateTranslator<XonePdpRB4JaguarNativeState, XoneGuitarState>
            .TranslateStateHandler s_Translator = TranslateState;

        protected override void FinishSetup()
        {
            base.FinishSetup();

            XoneSdlDescriptor.Verify(description.capabilities, "PDP Rock Band 4 Jaguar",
                buttonCount: 13,
                (offset: 4, usage: XoneSdlDescriptor.YUsage, hat: false),
                (offset: 8, usage: XoneSdlDescriptor.ZUsage, hat: false),
                (offset: 12, usage: XoneSdlDescriptor.Hat0XUsage, hat: true),
                (offset: 16, usage: XoneSdlDescriptor.Hat0YUsage, hat: true));
            XoneStateTranslator<XonePdpRB4JaguarNativeState, XoneGuitarState>.VerifyDevice(this);
        }

        private static XoneGuitarState TranslateState(ref XonePdpRB4JaguarNativeState state)
            => XoneGuitarTranslator.Translate(state.buttons, state.whammy, state.tilt, state.hatX, state.hatY,
                pickupSwitch: 0);

        void IInputStateCallbackReceiver.OnNextUpdate() {}

        void IInputStateCallbackReceiver.OnStateEvent(InputEventPtr eventPtr)
            => XoneStateTranslator<XonePdpRB4JaguarNativeState, XoneGuitarState>
                .OnStateEvent(this, eventPtr, s_Translator);

        bool IInputStateCallbackReceiver.GetStateOffsetForEvent(InputControl control, InputEventPtr eventPtr,
            ref uint offset)
            => XoneStateTranslator<XonePdpRB4JaguarNativeState, XoneGuitarState>
                .GetStateOffsetForEvent(this, control, eventPtr, ref offset, s_Translator);
    }
}
#endif
