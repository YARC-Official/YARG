using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using YARG.Input.Serialization;

namespace YARG.Input
{
    public class SingleAxisBinding : SingleBinding<float>
    {
        private float _invertSign = 1;
        private float _minimum;
        private float _maximum;

        private float _lowerDeadzone;
        private float _upperDeadzone;

        public bool Inverted
        {
            get => float.IsNegative(_invertSign);
            set
            {
                bool inverted = Inverted;
                _invertSign = value ? -1 : 1;

                // This state change won't be propogated to the main binding, however calibration settings
                // should never be changed outside of the binding menu, so that should be fine
                if (inverted != Inverted)
                {
                    State = CalculateState(RawState);
                    InvokeStateChanged(State);
                }
            }
        }

        public float Minimum
        {
            get => _minimum;
            set
            {
                _minimum = value;

                // (see above)
                State = CalculateState(RawState);
                InvokeStateChanged(State);
            }
        }

        public float Maximum
        {
            get => _maximum;
            set
            {
                _maximum = value;

                // (see above)
                State = CalculateState(RawState);
                InvokeStateChanged(State);
            }
        }

        public float LowerDeadzone
        {
            get => _lowerDeadzone;
            set
            {
                _lowerDeadzone = value;

                // (see above)
                State = CalculateState(RawState);
                InvokeStateChanged(State);
            }
        }

        public float UpperDeadzone
        {
            get => _upperDeadzone;
            set
            {
                _upperDeadzone = value;

                // (see above)
                State = CalculateState(RawState);
                InvokeStateChanged(State);
            }
        }

        public float RawState { get; private set; }

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
            if (!serialized.Parameters.TryGetValue(nameof(Inverted), out string invertedText) ||
                !bool.TryParse(invertedText, out bool inverted))
                inverted = false;

            Inverted = inverted;

            if (!serialized.Parameters.TryGetValue(nameof(Minimum), out string minText) ||
                !float.TryParse(minText, out float min))
                min = 0f;

            Minimum = min;

            if (!serialized.Parameters.TryGetValue(nameof(Maximum), out string maxText) ||
                !float.TryParse(maxText, out float max))
                max = 0;

            Maximum = max;

            if (!serialized.Parameters.TryGetValue(nameof(LowerDeadzone), out string lowerText) ||
                !float.TryParse(lowerText, out float lower))
                lower = 0f;

            LowerDeadzone = lower;

            if (!serialized.Parameters.TryGetValue(nameof(UpperDeadzone), out string upperText) ||
                !float.TryParse(upperText, out float upper))
                upper = 0f;

            UpperDeadzone = upper;
        }

        public override SerializedInputControl Serialize()
        {
            var serialized = base.Serialize();
            if (serialized is null)
                return null;

            serialized.Parameters.Add(nameof(Inverted), Inverted.ToString().ToLower());
            serialized.Parameters.Add(nameof(Minimum), Minimum.ToString());
            serialized.Parameters.Add(nameof(Maximum), Maximum.ToString());
            serialized.Parameters.Add(nameof(LowerDeadzone), LowerDeadzone.ToString());
            serialized.Parameters.Add(nameof(UpperDeadzone), UpperDeadzone.ToString());

            return serialized;
        }

        public override float UpdateState(InputEventPtr eventPtr)
        {
            if (!Control.HasValueChangeInEvent(eventPtr))
                return State;

            RawState = Control.ReadValueFromEvent(eventPtr);
            State = CalculateState(RawState);
            InvokeStateChanged(State);
            return State;
        }

        private float CalculateState(float rawValue)
        {
            float max;
            float min;
            float @base;

            if (rawValue > UpperDeadzone)
            {
                max = Maximum;
                min = UpperDeadzone;
                @base = 0;
            }
            else if (rawValue < LowerDeadzone)
            {
                max = LowerDeadzone;
                min = Minimum;
                @base = -1;
            }
            else
            {
                return 0;
            }

            float percentage = (rawValue - min) / (max - min);
            float value = @base + percentage;
            if (float.IsNaN(value))
                value = 0;

            value *= _invertSign;
            return value;
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