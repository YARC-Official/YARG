using System;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace YARG.Input
{
    public class IntegerBindingParameters
    {
    }

    public class IntegerBinding : ControlBinding<int, IntegerBindingParameters>
    {
        private int _currentValue;

        public IntegerBinding(string name, int action) : base(name, action)
        {
        }

        public override bool IsControlActuated(ActuationSettings settings, InputControl<int> control, InputEventPtr eventPtr)
        {
            int previousValue = control.ReadValueFromPreviousFrame();
            int value = control.ReadValue();
            if (control.HasValueChangeInEvent(eventPtr))
            {
                previousValue = value;
                value = control.ReadValueFromEvent(eventPtr);
            }

            return Math.Abs(value - previousValue) >= settings.IntegerDeltaThreshold;
        }

        public override void ProcessInputEvent(InputEventPtr eventPtr)
        {
            int max = 0;
            foreach (var binding in Bindings)
            {
                var value = binding.UpdateState(eventPtr);
                if (value > max)
                    max = value;
            }

            ProcessNextState(eventPtr.time, max);
        }

        private void ProcessNextState(double time, int state)
        {
            // Ignore if state is unchanged
            if (_currentValue == state)
                return;

            _currentValue = state;
            FireEvent(time, state);
        }
    }
}