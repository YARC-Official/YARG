using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using YARG.Input;
using YARG.Input.Bindings;

namespace YARG.Helpers.Extensions
{
    public static class InputExtensions
    {
        public static float GetPressPoint(this InputControl<float> control)
        {
            if (control is ButtonControl button)
                return button.pressPointOrDefault;

            return InputSystem.settings.defaultButtonPressPoint;
        }
    }
}