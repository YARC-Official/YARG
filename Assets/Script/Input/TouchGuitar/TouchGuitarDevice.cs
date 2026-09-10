using System.Runtime.InteropServices;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.Scripting;

namespace YARG.Input
{
    [Preserve]
    [StructLayout(LayoutKind.Sequential)]
    public struct TouchGuitarState : IInputStateTypeInfo
    {
        public const byte STRUM = 1 << 5;
        public const byte STAR_POWER = 1 << 6;

        public FourCC format => new('Y', 'T', 'G', 'T');

        [InputControl(name = "fret1", layout = "Button", bit = 0, displayName = "Green Fret")]
        [InputControl(name = "fret2", layout = "Button", bit = 1, displayName = "Red Fret")]
        [InputControl(name = "fret3", layout = "Button", bit = 2, displayName = "Yellow Fret")]
        [InputControl(name = "fret4", layout = "Button", bit = 3, displayName = "Blue Fret")]
        [InputControl(name = "fret5", layout = "Button", bit = 4, displayName = "Orange Fret")]
        [InputControl(name = "strum", layout = "Button", bit = 5, displayName = "Strum")]
        [InputControl(name = "starPower", layout = "Button", bit = 6, displayName = "Star Power")]
        public byte buttons;

        [InputControl(name = "whammy", layout = "Axis", displayName = "Whammy")]
        public byte whammy;
    }

    /// <summary>
    ///     A touchscreen instrument: touching the highway's fret lanes presses
    ///     those frets and strums on touch-down, touching above the lanes
    ///     fires star power, and tilting the phone works the whammy.
    ///     <see cref="TouchGuitarInput"/> feeds it during gameplay only, so
    ///     menus never see it, and it carries no menu binds. Profiles bind it
    ///     like any other controller.
    /// </summary>
    [Preserve]
    [InputControlLayout(stateType = typeof(TouchGuitarState), displayName = "Touch Controls")]
    public class TouchGuitarDevice : InputDevice
    {
        public const string INTERFACE_NAME = "YARGTouch";
        public const string PRODUCT_NAME = "Touch Controls";

        public ButtonControl fret1 { get; private set; }
        public ButtonControl fret2 { get; private set; }
        public ButtonControl fret3 { get; private set; }
        public ButtonControl fret4 { get; private set; }
        public ButtonControl fret5 { get; private set; }
        public ButtonControl strum { get; private set; }
        public ButtonControl starPower { get; private set; }
        public AxisControl whammy { get; private set; }

        public static TouchGuitarDevice Current { get; private set; }

        public static void Register()
        {
            InputSystem.RegisterLayout<TouchGuitarDevice>(
                matches: new InputDeviceMatcher().WithInterface(INTERFACE_NAME));
        }

        /// <summary>
        ///     Adds the device (once); a stable description lets profiles find
        ///     it again on later launches.
        /// </summary>
        public static TouchGuitarDevice Add()
        {
            if (Current == null || !Current.added)
            {
                Current = (TouchGuitarDevice) InputSystem.AddDevice(new InputDeviceDescription
                {
                    interfaceName = INTERFACE_NAME,
                    product = PRODUCT_NAME,
                });
            }

            return Current;
        }

        protected override void FinishSetup()
        {
            base.FinishSetup();
            fret1 = GetChildControl<ButtonControl>("fret1");
            fret2 = GetChildControl<ButtonControl>("fret2");
            fret3 = GetChildControl<ButtonControl>("fret3");
            fret4 = GetChildControl<ButtonControl>("fret4");
            fret5 = GetChildControl<ButtonControl>("fret5");
            strum = GetChildControl<ButtonControl>("strum");
            starPower = GetChildControl<ButtonControl>("starPower");
            whammy = GetChildControl<AxisControl>("whammy");
        }
    }
}
