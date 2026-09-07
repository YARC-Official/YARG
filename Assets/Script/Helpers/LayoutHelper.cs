using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using YARG.Input.Bindings;
using YARG.Menu.ProfileList;

namespace YARG.Helpers
{
    public static class LayoutHelper
    {
        private enum ControlInclusion
        {
            Introduces,
            Inherits,
            DoesNotHave
        }

        const string INPUT_DEVICE = nameof(InputDevice);

        public static InputControlLayout GetMostGeneralLayoutForControl(string layoutName, string controlName)
        {
            return GetMostGeneralLayoutForControl(InputSystem.LoadLayout(layoutName), controlName);
        }

        public static InputControlLayout GetMostGeneralLayoutForControl(InputDevice controller, string controlName)
        {
            return GetMostGeneralLayoutForControl(controller.layout, controlName);
        }

        public static InputControlLayout GetMostGeneralLayoutForControl(InputControlLayout controlSource, string controlName)
        {
            if (CheckControlInclusion(controlSource, controlName) is ControlInclusion.DoesNotHave)
            {
                throw new InvalidOperationException($"Layout {controlSource.name} does not have control {controlName}!");
            }

            var mostGeneralLayout = controlSource;

            while (CheckControlInclusion(mostGeneralLayout, controlName) is not ControlInclusion.Introduces)
            {
                mostGeneralLayout = GetParentLayout(mostGeneralLayout);
            }

            return mostGeneralLayout;
        }

        private static ControlInclusion CheckControlInclusion(InputControlLayout layout, string controlName)
        {
            foreach (var control in layout.controls)
            {
                if (control.name == controlName)
                {
                    return control.isFirstDefinedInThisLayout ? ControlInclusion.Introduces : ControlInclusion.Inherits;
                }
            }

            return ControlInclusion.DoesNotHave;
        }

        private static InputControlLayout GetParentLayout(InputControlLayout layout)
        {
            var parentName = InputSystem.GetNameOfBaseLayout(layout.name);
            return InputSystem.LoadLayout(parentName);
        }

        public static ControllerFamily LayoutStringToControllerFamily(string layout)
        {
            return layout switch
            {
                LayoutStrings.FIVE_FRET_GUITAR => ControllerFamily.FiveFretGuitar,
                LayoutStrings.SIX_FRET_GUITAR => ControllerFamily.SixFretGuitar,
                LayoutStrings.FOUR_LANE_DRUMKIT => ControllerFamily.FourLaneDrumkit,
                LayoutStrings.FIVE_LANE_DRUMKIT => ControllerFamily.FiveLaneDrumkit,
                LayoutStrings.PRO_KEYBOARD => ControllerFamily.ProKeyboard,
                LayoutStrings.PRO_GUITAR => ControllerFamily.ProGuitar,
                _ => ControllerFamily.Generic
            };
        }

        public static string ControllerFamilyToLayoutString(ControllerFamily controllerFamily)
        {
            return controllerFamily switch
            {
                ControllerFamily.FiveFretGuitar => LayoutStrings.FIVE_FRET_GUITAR,
                ControllerFamily.SixFretGuitar => LayoutStrings.SIX_FRET_GUITAR,
                ControllerFamily.FourLaneDrumkit => LayoutStrings.FOUR_LANE_DRUMKIT,
                ControllerFamily.FiveLaneDrumkit => LayoutStrings.FIVE_LANE_DRUMKIT,
                ControllerFamily.ProKeyboard => LayoutStrings.PRO_KEYBOARD,
                ControllerFamily.ProGuitar => LayoutStrings.PRO_GUITAR,
                _ => LayoutStrings.ANY
            };
        }

        public static string? GetDisplayNameOfControlInLayout(InputControlLayout layout, string name)
        {
            foreach (var control in layout.controls)
            {
                if (control.name == name)
                {
                    return control.displayName;
                }
            }

            return null;
        }
    }
}
