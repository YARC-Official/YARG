using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using YARG.Settings;

namespace YARG.Input
{
    public class AxisBindingParameters
    {
    }

    public class AxisBinding : ControlBinding<float, AxisBindingParameters>
    {
        private float _currentValue;

        public AxisBinding(string name, int action) : base(name, action)
        {
        }

        public override bool IsControlActuated(InputControl<float> control)
        {
            float previousValue = control.ReadValueFromPreviousFrame();
            float value = control.ReadValue();
            return !Mathf.Approximately(previousValue, value);
        }

        public override bool IsControlActuated(InputControl<float> control, InputEventPtr eventPtr)
        {
            float previousValue = control.ReadValueFromPreviousFrame();
            float value = control.ReadValue();
            if (control.HasValueChangeInEvent(eventPtr))
            {
                previousValue = value;
                value = control.ReadValueFromEvent(eventPtr);
            }

            return !Mathf.Approximately(previousValue, value);
        }

        public override void ProcessInputEvent(InputEventPtr eventPtr)
        {
            float max = 0f;
            foreach (var binding in Bindings)
            {
                var value = binding.UpdateState(eventPtr);
                if (value > max)
                    max = value;
            }

            ProcessNextState(eventPtr.time, max);
        }

        private void ProcessNextState(double time, float state)
        {
            // Ignore if state is unchanged
            if (Mathf.Approximately(_currentValue, state))
                return;

            _currentValue = state;
            FireEvent(time, state);
        }
    }
}