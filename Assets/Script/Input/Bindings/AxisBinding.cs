using System;
using UnityEngine;
using UnityEngine.InputSystem;
using YARG.Input.Serialization;

namespace YARG.Input
{
    public class SingleAxisBinding : SingleBinding<float>
    {
        private const bool INVERT_DEFAULT = false;

        private const float MINIMUM_DEFAULT = -1f;
        private const float MAXIMUM_DEFAULT = 1f;

        private const float LOWER_DEADZONE_DEFAULT = 0f;
        private const float UPPER_DEADZONE_DEFAULT = 0f;

        private float _invertSign = INVERT_DEFAULT ? -1 : 1;
        private float _minimum = MINIMUM_DEFAULT;
        private float _maximum = MAXIMUM_DEFAULT;

        private float _lowerDeadzone = LOWER_DEADZONE_DEFAULT;
        private float _upperDeadzone = UPPER_DEADZONE_DEFAULT;

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
                inverted = INVERT_DEFAULT;

            Inverted = inverted;

            if (!serialized.Parameters.TryGetValue(nameof(Minimum), out string minText) ||
                !float.TryParse(minText, out float min))
                min = MINIMUM_DEFAULT;

            Minimum = min;

            if (!serialized.Parameters.TryGetValue(nameof(Maximum), out string maxText) ||
                !float.TryParse(maxText, out float max))
                max = MAXIMUM_DEFAULT;

            Maximum = max;

            if (!serialized.Parameters.TryGetValue(nameof(LowerDeadzone), out string lowerText) ||
                !float.TryParse(lowerText, out float lower))
                lower = UPPER_DEADZONE_DEFAULT;

            LowerDeadzone = lower;

            if (!serialized.Parameters.TryGetValue(nameof(UpperDeadzone), out string upperText) ||
                !float.TryParse(upperText, out float upper))
                upper = LOWER_DEADZONE_DEFAULT;

            UpperDeadzone = upper;
        }

        public override SerializedInputControl Serialize()
        {
            var serialized = base.Serialize();
            if (serialized is null)
                return null;

            if (Inverted != INVERT_DEFAULT)
                serialized.Parameters.Add(nameof(Inverted), Inverted.ToString().ToLower());
            if (!Mathf.Approximately(Minimum, MINIMUM_DEFAULT))
                serialized.Parameters.Add(nameof(Minimum), Minimum.ToString());
            if (!Mathf.Approximately(Maximum, MAXIMUM_DEFAULT))
                serialized.Parameters.Add(nameof(Maximum), Maximum.ToString());
            if (!Mathf.Approximately(LowerDeadzone, UPPER_DEADZONE_DEFAULT))
                serialized.Parameters.Add(nameof(LowerDeadzone), LowerDeadzone.ToString());
            if (!Mathf.Approximately(UpperDeadzone, LOWER_DEADZONE_DEFAULT))
                serialized.Parameters.Add(nameof(UpperDeadzone), UpperDeadzone.ToString());

            return serialized;
        }

        public override void UpdateState(double time)
        {
            RawState = Control.value;
            State = CalculateState(RawState);
            InvokeStateChanged(State);
        }

        public override void ResetState()
        {
            RawState = default;
            State = default;
            InvokeStateChanged(State);
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
        public float State { get; protected set; }

        public AxisBinding(string name, int action) : base(name, action)
        {
        }

        public override bool IsControlActuated(ActuationSettings settings, InputControl<float> control)
        {
            float previousValue = control.ReadValueFromPreviousFrame();
            float value = control.ReadValue();

            return Math.Abs(value - previousValue) >= settings.AxisDeltaThreshold;
        }

        protected override void OnStateChanged(SingleAxisBinding _, double time)
        {
            float max = 0f;
            foreach (var binding in _bindings)
            {
                var value = binding.State;
                if (Math.Abs(value) > Math.Abs(max))
                    max = value;
            }

            // Ignore if state is unchanged
            if (Mathf.Approximately(State, max))
                return;

            State = max;
            FireInputEvent(time, max);
        }

        protected override SingleAxisBinding CreateBinding(ActuationSettings settings, InputControl<float> control)
        {
            return new(control, settings);
        }

        protected override SingleAxisBinding DeserializeControl(InputControl<float> control, SerializedInputControl serialized)
        {
            return new(control, serialized);
        }
    }
}