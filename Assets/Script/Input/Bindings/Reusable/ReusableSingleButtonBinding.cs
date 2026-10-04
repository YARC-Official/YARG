using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using YARG.Input.Serialization;

namespace YARG.Input.Bindings
{
    public class ReusableSingleButtonBinding : ReusableSingleBinding<float>
    {
        private const long DEBOUNCE_THRESHOLD_DEFAULT = 5;
        private const DebounceMode DEBOUNCE_MODE_DEFAULT = DebounceMode.Press;
        private const float PRESS_POINT_DEFAULT = 0.5f;
        private const bool INVERTED_DEFAULT = false;

        private long _debounceThreshold = DEBOUNCE_THRESHOLD_DEFAULT;
        public long DebounceThreshold {
            get => _debounceThreshold;
            set
            {
                if (_debounceThreshold != value)
                {
                    _debounceThreshold = value;
                    NotifyChanged();
                }
            }
        }

        private DebounceMode _debounceMode = DEBOUNCE_MODE_DEFAULT;
        public DebounceMode DebounceMode {
            get => _debounceMode;
            set
            {
                if (_debounceMode != value)
                {
                    _debounceMode = value;
                    NotifyChanged();
                }
            }
        }

        private float _pressPoint = PRESS_POINT_DEFAULT;
        public float PressPoint {
            get => _pressPoint;
            set
            {
                if (_pressPoint != value)
                {
                    _pressPoint = value;
                    NotifyChanged();
                }
            }
        }

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

        public ReusableSingleButtonBinding() : base(null, null, null)
        {
            DebounceThreshold = DEBOUNCE_THRESHOLD_DEFAULT;
            DebounceMode = DEBOUNCE_MODE_DEFAULT;
            PressPoint = PRESS_POINT_DEFAULT;
            Inverted = INVERTED_DEFAULT;
        }

        public ReusableSingleButtonBinding(ReusableSingleButtonBindingConfig config) : base(config.ControlPath, config.DisplayName, config.SourceLayout)
        {
            DebounceThreshold = config.DebounceThreshold ?? DEBOUNCE_THRESHOLD_DEFAULT;
            DebounceMode = config.DebounceMode ?? DEBOUNCE_MODE_DEFAULT;
            PressPoint = config.PressPoint ?? PRESS_POINT_DEFAULT;
            Inverted = config.Inverted ?? INVERTED_DEFAULT;
        }

        public ReusableSingleButtonBinding(ReusableSingleButtonBinding original) : base(original)
        {
            DebounceThreshold = original.DebounceThreshold;
            DebounceMode = original.DebounceMode;
            PressPoint = original.PressPoint;
            Inverted = original.Inverted;
        }

        public ReusableSingleButtonBinding(SerializedSingleBinding serialized) : base(serialized)
        {
            DeserializeParameters(serialized.Parameters);
        }

        protected override Dictionary<string, string> SerializeParameters()
        {
            var serialized = new Dictionary<string, string>();

            if (DebounceThreshold != DEBOUNCE_THRESHOLD_DEFAULT)
            {
                serialized[nameof(DebounceThreshold)] = DebounceThreshold.ToString();
            }

            if (DebounceMode != DEBOUNCE_MODE_DEFAULT)
            {
                serialized[nameof(DebounceMode)] = DebounceMode.ToString();
            }

            if (PressPoint != PRESS_POINT_DEFAULT)
            {
                serialized[nameof(PressPoint)] = PressPoint.ToString();
            }

            if (Inverted != INVERTED_DEFAULT)
            {
                serialized[nameof(Inverted)] = Inverted.ToString();
            }

            return serialized;
        }

        protected override void DeserializeParameters(Dictionary<string, string> parameters)
        {
            foreach (var (key, val) in parameters)
            {
                switch (key)
                {
                    case nameof(DebounceThreshold):
                        if (long.TryParse(val, out var debounceThreshold))
                        {
                            DebounceThreshold = debounceThreshold;
                        }
                        else
                        {
                            ReusableControlBinding.LogParseFailure(key, val, "bool", DEBOUNCE_THRESHOLD_DEFAULT);
                        }
                        break;

                    case nameof(DebounceMode):
                        if (Enum.TryParse<DebounceMode>(val, out var debounceMode))
                        {
                            DebounceMode = debounceMode;
                        }
                        else
                        {
                            ReusableControlBinding.LogParseFailure(key, val, "DebounceMode", DEBOUNCE_MODE_DEFAULT);
                        }
                        break;

                    case nameof(PressPoint):
                        if (float.TryParse(val, out var pressPoint))
                        {
                            PressPoint = pressPoint;
                        }
                        else
                        {
                            ReusableControlBinding.LogParseFailure(key, val, "float", PRESS_POINT_DEFAULT);
                        }
                        break;

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

                    default:
                        ReusableControlBinding.LogUnknownParameter(key, val);
                        break;
                }
            }
        }

        protected override RuntimeSingleBinding<float> MakeRuntime(InputControl<float> control)
        {
            return new RuntimeSingleButtonBinding(control, this);
        }
    }
}
