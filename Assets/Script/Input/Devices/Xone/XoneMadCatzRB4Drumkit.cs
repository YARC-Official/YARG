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
    /// The native state of the Mad Catz Rock Band 4 drum kit as reported by the xone
    /// <c>xone_gip_madcatz_glam</c> kernel driver through Unity's Linux joystick support.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct XoneMadCatzRB4DrumkitNativeState : IInputStateTypeInfo
    {
        public FourCC format => XoneLinuxState.Format;

        // Buttons in ascending Linux key code order:
        // A, B, X, Y, select, start, guide, kick 1, kick 2,
        // red pad, yellow pad, blue pad, green pad,
        // yellow cymbal, blue cymbal, green cymbal
        public const int SouthBit = 1 << 0;
        public const int EastBit = 1 << 1;
        public const int WestBit = 1 << 2;
        public const int NorthBit = 1 << 3;
        public const int SelectBit = 1 << 4;
        public const int StartBit = 1 << 5;
        public const int SystemBit = 1 << 6;
        public const int Kick1Bit = 1 << 7;
        public const int Kick2Bit = 1 << 8;
        public const int RedPadBit = 1 << 9;
        public const int YellowPadBit = 1 << 10;
        public const int BluePadBit = 1 << 11;
        public const int GreenPadBit = 1 << 12;
        public const int YellowCymbalBit = 1 << 13;
        public const int BlueCymbalBit = 1 << 14;
        public const int GreenCymbalBit = 1 << 15;

#pragma warning disable CS0649
        public ushort buttons;
        public ushort padding; // Required for state alignment, never accessed in code
        public int hatX;
        public int hatY;
#pragma warning restore CS0649
    }

    /// <summary>
    /// The state which the xone Mad Catz Rock Band 4 drum kit is translated into.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct XoneMadCatzRB4DrumkitState : IInputStateTypeInfo
    {
        public static FourCC Format => new('X', 'D', 'R', 'M');
        public FourCC format => Format;

        [InputControl(name = "buttonSouth", layout = "Button", bit = 0)]
        [InputControl(name = "buttonEast", layout = "Button", bit = 1)]
        [InputControl(name = "buttonWest", layout = "Button", bit = 2)]
        [InputControl(name = "buttonNorth", layout = "Button", bit = 3)]

        [InputControl(name = "dpad", layout = "Dpad", format = "BIT", bit = 4, sizeInBits = 4)]
        [InputControl(name = "dpad/up", bit = 4)]
        [InputControl(name = "dpad/down", bit = 5)]
        [InputControl(name = "dpad/left", bit = 6)]
        [InputControl(name = "dpad/right", bit = 7)]

        [InputControl(name = "startButton", layout = "Button", bit = 8)]
        [InputControl(name = "selectButton", layout = "Button", bit = 9)]
        [InputControl(name = "systemButton", layout = "Button", bit = 10)]

        [InputControl(name = "kick1", layout = "Button", bit = 11)]
        [InputControl(name = "kick2", layout = "Button", bit = 12)]
        [InputControl(name = "redPad", layout = "Button", bit = 13)]
        [InputControl(name = "yellowPad", layout = "Button", bit = 14)]
        [InputControl(name = "bluePad", layout = "Button", bit = 15)]
        public ushort buttons;

        [InputControl(name = "greenPad", layout = "Button", bit = 0)]
        [InputControl(name = "yellowCymbal", layout = "Button", bit = 1)]
        [InputControl(name = "blueCymbal", layout = "Button", bit = 2)]
        [InputControl(name = "greenCymbal", layout = "Button", bit = 3)]
        public ushort pads;
    }

    /// <summary>
    /// The Mad Catz Rock Band 4 drum kit, when driven by the xone kernel driver on Linux.
    /// </summary>
    [InputControlLayout(stateType = typeof(XoneMadCatzRB4DrumkitState),
        displayName = "Mad Catz Rock Band 4 Drumkit (xone)")]
    internal class XoneMadCatzRB4Drumkit : FourLaneDrumkit, IInputStateCallbackReceiver
    {
        internal const string NativeProduct = "Mad Catz Rock Band 4 Drum Kit";

        internal static void Initialize()
        {
            InputSystem.RegisterLayout<XoneMadCatzRB4Drumkit>();
            InputSystem.RegisterLayoutMatcher(nameof(XoneMadCatzRB4Drumkit),
                new InputDeviceMatcher().WithProduct("^" + NativeProduct + "$"));
        }

        private static XoneStateTranslator<XoneMadCatzRB4DrumkitNativeState, XoneMadCatzRB4DrumkitState>
            .TranslateStateHandler s_Translator = TranslateState;

        protected override void FinishSetup()
        {
            base.FinishSetup();

            XoneSdlDescriptor.Verify(description.capabilities, "Mad Catz Rock Band 4 drum kit",
                buttonCount: 16,
                (offset: 4, usage: XoneSdlDescriptor.Hat0XUsage, hat: true),
                (offset: 8, usage: XoneSdlDescriptor.Hat0YUsage, hat: true));
            XoneStateTranslator<XoneMadCatzRB4DrumkitNativeState, XoneMadCatzRB4DrumkitState>.VerifyDevice(this);
        }

        private static XoneMadCatzRB4DrumkitState TranslateState(
            ref XoneMadCatzRB4DrumkitNativeState state)
        {
            ushort buttons = state.buttons;

            ushort translated = 0;
            if ((buttons & XoneMadCatzRB4DrumkitNativeState.SouthBit) != 0) translated |= 1 << 0;
            if ((buttons & XoneMadCatzRB4DrumkitNativeState.EastBit) != 0) translated |= 1 << 1;
            if ((buttons & XoneMadCatzRB4DrumkitNativeState.WestBit) != 0) translated |= 1 << 2;
            if ((buttons & XoneMadCatzRB4DrumkitNativeState.NorthBit) != 0) translated |= 1 << 3;
            if (state.hatY < 0) translated |= 1 << 4;
            if (state.hatY > 0) translated |= 1 << 5;
            if (state.hatX < 0) translated |= 1 << 6;
            if (state.hatX > 0) translated |= 1 << 7;
            if ((buttons & XoneMadCatzRB4DrumkitNativeState.StartBit) != 0) translated |= 1 << 8;
            if ((buttons & XoneMadCatzRB4DrumkitNativeState.SelectBit) != 0) translated |= 1 << 9;
            if ((buttons & XoneMadCatzRB4DrumkitNativeState.SystemBit) != 0) translated |= 1 << 10;
            if ((buttons & XoneMadCatzRB4DrumkitNativeState.Kick1Bit) != 0) translated |= 1 << 11;
            if ((buttons & XoneMadCatzRB4DrumkitNativeState.Kick2Bit) != 0) translated |= 1 << 12;
            if ((buttons & XoneMadCatzRB4DrumkitNativeState.RedPadBit) != 0) translated |= 1 << 13;
            if ((buttons & XoneMadCatzRB4DrumkitNativeState.YellowPadBit) != 0) translated |= 1 << 14;
            if ((buttons & XoneMadCatzRB4DrumkitNativeState.BluePadBit) != 0) translated |= 1 << 15;

            ushort pads = 0;
            if ((buttons & XoneMadCatzRB4DrumkitNativeState.GreenPadBit) != 0) pads |= 1 << 0;
            if ((buttons & XoneMadCatzRB4DrumkitNativeState.YellowCymbalBit) != 0) pads |= 1 << 1;
            if ((buttons & XoneMadCatzRB4DrumkitNativeState.BlueCymbalBit) != 0) pads |= 1 << 2;
            if ((buttons & XoneMadCatzRB4DrumkitNativeState.GreenCymbalBit) != 0) pads |= 1 << 3;

            return new XoneMadCatzRB4DrumkitState
            {
                buttons = translated,
                pads = pads,
            };
        }

        void IInputStateCallbackReceiver.OnNextUpdate() {}

        void IInputStateCallbackReceiver.OnStateEvent(InputEventPtr eventPtr)
            => XoneStateTranslator<XoneMadCatzRB4DrumkitNativeState, XoneMadCatzRB4DrumkitState>
                .OnStateEvent(this, eventPtr, s_Translator);

        bool IInputStateCallbackReceiver.GetStateOffsetForEvent(InputControl control, InputEventPtr eventPtr,
            ref uint offset)
            => XoneStateTranslator<XoneMadCatzRB4DrumkitNativeState, XoneMadCatzRB4DrumkitState>
                .GetStateOffsetForEvent(this, control, eventPtr, ref offset, s_Translator);
    }
}
#endif
