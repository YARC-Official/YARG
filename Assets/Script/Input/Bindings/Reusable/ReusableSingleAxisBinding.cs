using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.InputSystem;
using YARG.Input.Serialization;

namespace YARG.Input.Bindings
{
    public class ReusableSingleAxisBinding : ReusableSingleBinding<float>
    {
        private const bool INVERTED_DEFAULT = false;
        private const float MINIMUM_DEFAULT = -1f;
        private const float MAXIMUM_DEFAULT = 1f;
        private const float LOWER_DEADZONE_DEFAULT = 0f;
        private const float UPPER_DEADZONE_DEFAULT = 0f;

        private bool _inverted = INVERTED_DEFAULT;
        public bool Inverted {
            get => _inverted;
            set
            {
                if (_inverted != value)
                {
                    _inverted = value;
                    NotifyChanged();
                }
            }
        }


        private float _maximum = MAXIMUM_DEFAULT;
        public float Maximum {
            get => _maximum;
            set
            {
                if (_maximum != value)
                {
                    _maximum = value;
                    NotifyChanged();
                }
            }
        }

        private float _minimum = MINIMUM_DEFAULT;
        public float Minimum
        {
            get => _minimum;
            set
            {
                if (_minimum != value)
                {
                    _minimum = value;
                    NotifyChanged();
                }
            }
        }

        private float _lowerDeadzone = LOWER_DEADZONE_DEFAULT;
        public float LowerDeadzone {
            get => _lowerDeadzone;
            set
            {
                if (_lowerDeadzone != value)
                {
                    _lowerDeadzone = value;
                    NotifyChanged();
                }
            }
        }

        private float _upperDeadzone = UPPER_DEADZONE_DEFAULT;
        public float UpperDeadzone {
            get => _upperDeadzone;
            set
            {
                if (_upperDeadzone != value)
                {
                    _upperDeadzone = value;
                    NotifyChanged();
                }
            }
        }

        public ReusableSingleAxisBinding() : base(null, null, null) {}

        public ReusableSingleAxisBinding(ReusableSingleAxisBindingConfig control) : base(control.ControlPath, control.DisplayName, control.SourceLayout) { }

        public ReusableSingleAxisBinding(SerializedSingleBinding serialized) : base(serialized)
        {
            DeserializeParameters(serialized.Parameters);
        }

        public ReusableSingleAxisBinding(ReusableSingleAxisBinding original) : base(original)
        {
            Inverted = original.Inverted;
            Maximum = original.Maximum;
            Minimum = original.Minimum;
            LowerDeadzone = original.LowerDeadzone;
            UpperDeadzone = original.UpperDeadzone;
        }

        protected override Dictionary<string, string> SerializeParameters()
        {
            var serialized = new Dictionary<string, string>();

            if (Inverted != INVERTED_DEFAULT)
            {
                serialized[nameof(Inverted)] = Inverted.ToString();
            }

            if (Minimum != MINIMUM_DEFAULT)
            {
                serialized[nameof(Minimum)] = Minimum.ToString();
            }

            if (Maximum != MAXIMUM_DEFAULT)
            {
                serialized[nameof(Maximum)] = Maximum.ToString();
            }

            if (UpperDeadzone != UPPER_DEADZONE_DEFAULT)
            {
                serialized[nameof(UpperDeadzone)] = UpperDeadzone.ToString();
            }

            if (LowerDeadzone != LOWER_DEADZONE_DEFAULT)
            {
                serialized[nameof(LowerDeadzone)] = LowerDeadzone.ToString();
            }

            return serialized;
        }

        protected override void DeserializeParameters(Dictionary<string, string> parameters)
        {
            foreach (var (key, val) in parameters)
            {
                switch (key)
                {
                    case nameof(Inverted):
                        if (bool.TryParse(val, out var inverted))
                        {
                            Inverted = inverted;
                        }
                        else
                        {
                            ReusableControlBinding.LogParseFailure(key, val, "bool", INVERTED_DEFAULT);
                        }
                        break;

                    case nameof(Maximum):
                        if (float.TryParse(val, out var maximum))
                        {
                            Maximum = maximum;
                        }
                        else
                        {
                            ReusableControlBinding.LogParseFailure(key, val, "float", MAXIMUM_DEFAULT);
                        }
                        break;

                    case nameof(Minimum):
                        if (float.TryParse(val, out var minimum))
                        {
                            Minimum = minimum;
                        }
                        else
                        {
                            ReusableControlBinding.LogParseFailure(key, val, "float", MINIMUM_DEFAULT);
                        }
                        break;

                    case nameof(UpperDeadzone):
                        if (float.TryParse(val, out var upperDeadzone))
                        {
                            UpperDeadzone = upperDeadzone;
                        }
                        else
                        {
                            ReusableControlBinding.LogParseFailure(key, val, "float", UPPER_DEADZONE_DEFAULT);
                        }
                        break;

                    case nameof(LowerDeadzone):
                        if (float.TryParse(val, out var lowerDeadzone))
                        {
                            LowerDeadzone = lowerDeadzone;
                        }
                        else
                        {
                            ReusableControlBinding.LogParseFailure(key, val, "float", LOWER_DEADZONE_DEFAULT);
                        }
                        break;

                    default:
                        ReusableControlBinding.LogUnknownParameter(key, val);
                        break;
                }
            }
        }

        protected override bool IsControlActuated(InputControl<float> control)
        {
            float previousValue = control.ReadValueFromPreviousFrame();
            float value = control.ReadValue();

            return Math.Abs(value - previousValue) >= RuntimeControlBinding.AXIS_DELTA_THRESHOLD;
        }

        protected override RuntimeSingleBinding<float> MakeRuntime(InputControl<float> control)
        {
            return new RuntimeSingleAxisBinding(control, this);
        }
    }
}
