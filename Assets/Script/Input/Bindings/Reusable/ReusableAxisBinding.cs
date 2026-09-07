using System;
using System.Collections.Generic;
using System.Text;
using YARG.Input.Serialization;

namespace YARG.Input.Bindings
{
    public class ReusableAxisBinding : ReusableControlBinding<ReusableSingleAxisBinding>
    {
        public ReusableAxisBinding(InputActionInfo info) : base(info) { }

        public ReusableAxisBinding(SerializedReusableControlBinding serialized, InputActionInfo info) : base(serialized, info) {
            foreach (var binding in serialized.Controls)
            {
                Bindings.Add(new(binding));
            }
        }
    }

    public class ReusableSingleAxisBinding : ReusableSingleBinding
    {
        private const bool INVERTED_DEFAULT = false;

        private const float MINIMUM_DEFAULT = -1f;
        private const float MAXIMUM_DEFAULT = 1f;

        private const float LOWER_DEADZONE_DEFAULT = 0f;
        private const float UPPER_DEADZONE_DEFAULT = 0f;

        public bool Inverted { get; set; }
        public float Maximum { get; set; }
        public float Minimum { get; set; }
        public float LowerDeadzone { get; set; }
        public float UpperDeadzone { get; set; }

        public ReusableSingleAxisBinding(SerializedSingleBinding serialized)
        {
            DeserializeParameters(serialized.Parameters);
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
    }
}
