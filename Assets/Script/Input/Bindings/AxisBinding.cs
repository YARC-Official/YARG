using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using YARG.Input.Serialization;

namespace YARG.Input
{
    public class SingleAxisBinding : SingleBinding<float>
    {
        public SingleAxisBinding(InputControl<float> control) : base(control)
        {
        }

        public SingleAxisBinding(InputControl<float> control, ActuationSettings settings)
            : base(control)
        {
        }

        public SingleAxisBinding(InputControl<float> control, SerializedInputControl serialized)
            : base(control, serialized)
        {
        }

        public override SerializedInputControl Serialize()
        {
            return base.Serialize();
        }
    }

    public class AxisBinding : ControlBinding<float, SingleAxisBinding>
    {
        private float _currentValue;

        public AxisBinding(string name, int action) : base(name, action)
        {
        }

        public override bool IsControlActuated(ActuationSettings settings, InputControl<float> control, InputEventPtr eventPtr)
        {
            if (!control.HasValueChangeInEvent(eventPtr))
                return false;

            // The buffer that ReadValue reads from is not updated until after all events have been processed
            float previousValue = control.ReadValue();
            float value = control.ReadValueFromEvent(eventPtr);

            return Math.Abs(value - previousValue) >= settings.AxisDeltaThreshold;
        }

        public override void ProcessInputEvent(InputEventPtr eventPtr)
        {
            float max = 0f;
            foreach (var binding in _bindings)
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
            FireInputEvent(time, state);
        }

        protected override SingleAxisBinding OnControlAdded(ActuationSettings settings, InputControl<float> control)
        {
            return new(control, settings);
        }

        protected override SingleAxisBinding DeserializeControl(InputControl<float> control, SerializedInputControl serialized)
        {
            return new(control, serialized);
        }
    }
}