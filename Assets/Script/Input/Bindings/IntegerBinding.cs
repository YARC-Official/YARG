using System;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using YARG.Input.Serialization;

namespace YARG.Input
{
    public class SingleIntegerBinding : SingleBinding<int>
    {
        public SingleIntegerBinding(InputControl<int> control) : base(control)
        {
        }

        public SingleIntegerBinding(InputControl<int> control, ActuationSettings settings)
            : base(control)
        {
        }

        public SingleIntegerBinding(InputControl<int> control, SerializedInputControl serialized)
            : base(control, serialized)
        {
        }

        public override SerializedInputControl Serialize()
        {
            return base.Serialize();
        }
    }

    public class IntegerBinding : ControlBinding<int, SingleIntegerBinding>
    {
        private int _currentValue;

        public IntegerBinding(string name, int action) : base(name, action)
        {
        }

        public override bool IsControlActuated(ActuationSettings settings, InputControl<int> control, InputEventPtr eventPtr)
        {
            if (!control.HasValueChangeInEvent(eventPtr))
                return false;

            // The buffer that ReadValue reads from is not updated until after all events have been processed
            int previousValue = control.ReadValue();
            int value = control.ReadValueFromEvent(eventPtr);

            return Math.Abs(value - previousValue) >= settings.IntegerDeltaThreshold;
        }

        public override void ProcessInputEvent(InputEventPtr eventPtr)
        {
            int max = 0;
            foreach (var binding in _bindings)
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
            FireInputEvent(time, state);
        }

        protected override SingleIntegerBinding OnControlAdded(ActuationSettings settings, InputControl<int> control)
        {
            return new(control, settings);
        }

        protected override SingleIntegerBinding DeserializeControl(InputControl<int> control, SerializedInputControl serialized)
        {
            return new(control, serialized);
        }
    }
}