using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using YARG.Input;

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

        public static float GetPressPoint(this InputControl<float> control, ActuationSettings settings)
        {
            // Explicitly-set press points take precedence over defaults
            if (control is ButtonControl button && button.pressPoint >= 0)
                return button.pressPoint;

            return settings.ButtonPressThreshold;
        }
    }
}