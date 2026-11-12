#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

namespace YARG.Input.Devices
{
    /// <summary>
    /// Helpers for devices which arrive as native Linux joystick devices (interface "Linux").
    /// <br/>
    /// The native joystick support describes devices with an SDL feature descriptor
    /// (see <see cref="XoneSdlDescriptor"/>) and sends state in the "LJOY" format:
    /// button features packed into bytes at their descriptor offsets, followed by
    /// 32-bit integer axis and hat features at 4-byte aligned offsets.
    /// </summary>
    internal static class XoneLinuxState
    {
        /// <summary>
        /// The state format used by Unity's native Linux joystick support.
        /// </summary>
        public static readonly FourCC Format = new('L', 'J', 'O', 'Y');
    }

    /// <summary>
    /// The SDL feature descriptor which the native Linux joystick support reports
    /// in a device description's capabilities.
    /// </summary>
    [Serializable]
    internal class XoneSdlDescriptor
    {
        // SDL feature types
        public const int AxisType = 1;
        public const int ButtonType = 3;
        public const int HatType = 4;

        // SDL axis usages
        public const int XUsage = 1;
        public const int YUsage = 2;
        public const int ZUsage = 3;
        public const int Hat0XUsage = 12;
        public const int Hat0YUsage = 13;

#pragma warning disable CS0649
        public Control[] controls;
#pragma warning restore CS0649

        [Serializable]
        public struct Control
        {
#pragma warning disable CS0649
            public int featureType;
            public int usageHint;
            public int featureSize;
            public int offset;
            public int bit;
            public int min;
            public int max;
#pragma warning restore CS0649
        }

        public static bool TryParse(string capabilities, out XoneSdlDescriptor descriptor)
        {
            descriptor = null;
            if (string.IsNullOrEmpty(capabilities))
                return false;

            try
            {
                descriptor = JsonUtility.FromJson<XoneSdlDescriptor>(capabilities);
            }
            catch (Exception)
            {
                return false;
            }

            return descriptor?.controls != null;
        }

        public static bool IsButton(in Control control, int index)
            => control.featureType == ButtonType && control.featureSize == 1 &&
                control.offset == index / 8 && control.bit == index % 8;

        public static bool IsAxis(in Control control, int offset, int usage)
            => control.featureType == AxisType && control.featureSize == 4 &&
                control.offset == offset && control.usageHint == usage;

        public static bool IsHat(in Control control, int offset, int usage)
            => control.featureType == HatType && control.featureSize == 4 &&
                control.offset == offset && control.usageHint == usage;

        /// <summary>
        /// Verifies that the descriptor matches the expected layout, so that devices
        /// with different report formats fail loudly instead of being mis-mapped.
        /// </summary>
        public static void Verify(string capabilities, string deviceName,
            int buttonCount, params (int offset, int usage, bool hat)[] analogControls)
        {
            if (!TryParse(capabilities, out var descriptor))
                throw new NotSupportedException($"Missing or invalid descriptor on xone {deviceName}.");

            var controls = descriptor.controls;
            if (controls.Length != buttonCount + analogControls.Length)
                throw new NotSupportedException($"Unexpected descriptor on xone {deviceName}.");

            for (int i = 0; i < buttonCount; i++)
            {
                if (!IsButton(controls[i], i))
                    throw new NotSupportedException($"Unexpected descriptor on xone {deviceName}.");
            }

            for (int i = 0; i < analogControls.Length; i++)
            {
                var (offset, usage, hat) = analogControls[i];
                var control = controls[buttonCount + i];
                bool matches = hat
                    ? IsHat(control, offset, usage)
                    : IsAxis(control, offset, usage);
                if (!matches)
                    throw new NotSupportedException($"Unexpected descriptor on xone {deviceName}.");
            }
        }
    }

    /// <summary>
    /// Translates a device's native state from <typeparamref name="TFromState"/> into
    /// <typeparamref name="TToState"/> in-place on incoming state events.
    /// <br/>
    /// Mirrors PlasticBand's internal state translation, which is not accessible from here.
    /// </summary>
    internal static unsafe class XoneStateTranslator<TFromState, TToState>
        where TFromState : unmanaged, IInputStateTypeInfo
        where TToState : unmanaged, IInputStateTypeInfo
    {
        internal delegate TToState TranslateStateHandler(ref TFromState fromState);

        private static readonly FourCC s_FromFormat = default(TFromState).format;
        private static readonly FourCC s_ToFormat = default(TToState).format;

        public static void VerifyDevice(InputDevice device)
        {
            if (device.stateBlock.format != s_ToFormat)
                throw new NotSupportedException($"Device state format must be {s_ToFormat} ({typeof(TToState).Name})!");

            if (sizeof(TFromState) < sizeof(TToState))
                throw new NotSupportedException(
                    $"Input state size is too small for reports translated into {s_ToFormat} ({typeof(TToState).Name})!");
        }

        public static void OnStateEvent(InputDevice device, InputEventPtr eventPtr,
            TranslateStateHandler translator)
        {
            if (eventPtr.handled || !TranslateState(eventPtr, translator))
                return;

            InputState.Change(device, eventPtr);
            eventPtr.handled = true;
        }

        public static bool GetStateOffsetForEvent(InputDevice device, InputControl control, InputEventPtr eventPtr,
            ref uint offset, TranslateStateHandler translator)
        {
            if ((control != null && control.device != device) || !TranslateState(eventPtr, translator))
                return false;

            // No offset is required, all controls read relative to the start of the device's state block
            offset = 0;
            return true;
        }

        private static bool TranslateState(InputEventPtr eventPtr, TranslateStateHandler translator)
        {
            if (eventPtr.type != StateEvent.Type)
                return false;

            var stateEvent = StateEvent.From(eventPtr);

            // Skip if this event has already been translated
            if (stateEvent->stateFormat == s_ToFormat)
                return true;

            if (stateEvent->stateFormat != s_FromFormat)
                return false;

            if (stateEvent->stateSizeInBytes < sizeof(TFromState) || stateEvent->stateSizeInBytes < sizeof(TToState))
                return false;

            ref TFromState fromState = ref *(TFromState*)stateEvent->state;
            *(TToState*)stateEvent->state = translator(ref fromState);
            stateEvent->stateFormat = s_ToFormat;
            return true;
        }
    }

    /// <summary>
    /// The state which xone-driven Rock Band guitars are translated into.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct XoneGuitarState : IInputStateTypeInfo
    {
        public static FourCC Format => new('X', 'G', 'T', 'R');
        public FourCC format => Format;

        [InputControl(name = "dpad", layout = "Dpad", format = "BIT", bit = 0, sizeInBits = 4)]
        [InputControl(name = "dpad/up", bit = 0)]
        [InputControl(name = "dpad/down", bit = 1)]
        [InputControl(name = "dpad/left", bit = 2)]
        [InputControl(name = "dpad/right", bit = 3)]

        [InputControl(name = "startButton", layout = "Button", bit = 5)]
        [InputControl(name = "selectButton", layout = "Button", bit = 6)]
        [InputControl(name = "systemButton", layout = "Button", bit = 7)]
        public ushort buttons;

        [InputControl(name = "greenFret", layout = "Button", bit = 0)]
        [InputControl(name = "redFret", layout = "Button", bit = 1)]
        [InputControl(name = "yellowFret", layout = "Button", bit = 2)]
        [InputControl(name = "blueFret", layout = "Button", bit = 3)]
        [InputControl(name = "orangeFret", layout = "Button", bit = 4)]
        public byte frets;

        [InputControl(name = "soloGreen", layout = "Button", bit = 0)]
        [InputControl(name = "soloRed", layout = "Button", bit = 1)]
        [InputControl(name = "soloYellow", layout = "Button", bit = 2)]
        [InputControl(name = "soloBlue", layout = "Button", bit = 3)]
        [InputControl(name = "soloOrange", layout = "Button", bit = 4)]
        public byte soloFrets;

        [InputControl(name = "whammy", layout = "IntAxis", parameters = "minValue=0x00,maxValue=0xFF,zeroPoint=0x00")]
        public byte whammy;

        [InputControl(name = "tilt", layout = "IntAxis", noisy = true, parameters = "minValue=-128,maxValue=127,zeroPoint=0")]
        public sbyte tilt;

        [InputControl(name = "pickupSwitch", layout = "Integer")]
        public byte pickupSwitch;
    }

    /// <summary>
    /// Common translation logic for xone-driven Rock Band guitars.
    /// </summary>
    internal static class XoneGuitarTranslator
    {
        // Button bits shared by all xone guitar drivers, in ascending Linux key code order
        public const int SelectBit = 1 << 0;
        public const int StartBit = 1 << 1;
        public const int SystemBit = 1 << 2;
        public const int GreenFretBit = 1 << 3;
        public const int RedFretBit = 1 << 4;
        public const int YellowFretBit = 1 << 5;
        public const int BlueFretBit = 1 << 6;
        public const int OrangeFretBit = 1 << 7;
        public const int SoloGreenBit = 1 << 8;
        public const int SoloRedBit = 1 << 9;
        public const int SoloYellowBit = 1 << 10;
        public const int SoloBlueBit = 1 << 11;
        public const int SoloOrangeBit = 1 << 12;

        public static XoneGuitarState Translate(ushort buttons, int whammy, int tilt, int hatX, int hatY,
            byte pickupSwitch)
        {
            ushort translated = 0;
            if (hatY < 0) translated |= 1 << 0;
            if (hatY > 0) translated |= 1 << 1;
            if (hatX < 0) translated |= 1 << 2;
            if (hatX > 0) translated |= 1 << 3;
            if ((buttons & StartBit) != 0) translated |= 1 << 5;
            if ((buttons & SelectBit) != 0) translated |= 1 << 6;
            if ((buttons & SystemBit) != 0) translated |= 1 << 7;

            byte frets = 0;
            if ((buttons & GreenFretBit) != 0) frets |= 1 << 0;
            if ((buttons & RedFretBit) != 0) frets |= 1 << 1;
            if ((buttons & YellowFretBit) != 0) frets |= 1 << 2;
            if ((buttons & BlueFretBit) != 0) frets |= 1 << 3;
            if ((buttons & OrangeFretBit) != 0) frets |= 1 << 4;

            byte soloFrets = 0;
            if ((buttons & SoloGreenBit) != 0) soloFrets |= 1 << 0;
            if ((buttons & SoloRedBit) != 0) soloFrets |= 1 << 1;
            if ((buttons & SoloYellowBit) != 0) soloFrets |= 1 << 2;
            if ((buttons & SoloBlueBit) != 0) soloFrets |= 1 << 3;
            if ((buttons & SoloOrangeBit) != 0) soloFrets |= 1 << 4;

            return new XoneGuitarState
            {
                buttons = translated,
                frets = frets,
                soloFrets = soloFrets,
                whammy = (byte) Mathf.Clamp(whammy, 0, 255),
                tilt = (sbyte) (Mathf.Clamp(tilt, 0, 255) >> 1),
                pickupSwitch = pickupSwitch,
            };
        }
    }
}
#endif
