using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;

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
    }
}
