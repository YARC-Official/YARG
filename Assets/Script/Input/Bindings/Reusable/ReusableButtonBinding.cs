using System;
using System.Collections.Generic;
using System.Text;
using YARG.Helpers;
using YARG.Input.Serialization;
using YARG.Menu.ProfileList;

namespace YARG.Input.Bindings
{
    public class ReusableButtonBinding : ReusableControlBinding<ReusableSingleButtonBinding>
    {
        private const long DEBOUNCE_THRESHOLD_DEFAULT = 5;
        public long DebounceThreshold { get; set; }

        public ReusableButtonBinding(InputActionInfo info) : base(info) { }

        public ReusableButtonBinding(InputActionInfo info, ReusableSingleButtonBindingConfig control)
            : this(info, new List<ReusableSingleButtonBindingConfig>() { control }) { }

        public ReusableButtonBinding(InputActionInfo info, List<ReusableSingleButtonBindingConfig> controls) : base(info)
        {
            foreach (var controlConfig in controls)
            {
                Bindings.Add(new(controlConfig));
            }
        }

        public ReusableButtonBinding(SerializedReusableControlBinding serialized, InputActionInfo info) : base(serialized, info) {
            foreach (var binding in serialized.Controls)
            {
                Bindings.Add(new(binding));
            }
        }

        protected override Dictionary<string, string> SerializeParameters()
        {
            var serialized = new Dictionary<string, string>();

            if (DebounceThreshold != DEBOUNCE_THRESHOLD_DEFAULT)
            {
                serialized[nameof(DebounceThreshold)] = DebounceThreshold.ToString();
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
                            LogParseFailure(key, val, "bool", DEBOUNCE_THRESHOLD_DEFAULT);
                        }
                        break;

                    default:
                        LogUnknownParameter(key, val);
                        break;
                }
            }
        }
    }

    public struct ReusableSingleButtonBindingConfig
    {
        public string ControlPath;
        public string DisplayName;
        public long? DebounceThreshold;
        public DebounceMode? DebounceMode;
        public float? PressPoint;
        public bool? Inverted;

        public ReusableSingleButtonBindingConfig(ControllerFamily family, string controlPath)
        {
            ControlItemInfo control = LayoutHelper.GetControlInfo(family, controlPath);

            ControlPath = control.ControlPath;
            DisplayName = control.DisplayName;

            // These would be more pleasant as struct field initializers, but those aren't in C# 9.0
            DebounceThreshold = null;
            DebounceMode = null;
            PressPoint = null;
            Inverted = null;
        }
    }

    public class ReusableSingleButtonBinding : ReusableSingleBinding
    {
        private const long DEBOUNCE_THRESHOLD_DEFAULT = 5;
        private const DebounceMode DEBOUNCE_MODE_DEFAULT = DebounceMode.Press;
        private const float PRESS_POINT_DEFAULT = 0.5f;
        private const bool INVERTED_DEFAULT = false;

        public long DebounceThreshold { get; set; } = DEBOUNCE_THRESHOLD_DEFAULT;
        public DebounceMode DebounceMode { get; set; } = DEBOUNCE_MODE_DEFAULT;
        public float PressPoint { get; set; } = PRESS_POINT_DEFAULT;
        public bool Inverted { get; set; } = INVERTED_DEFAULT;

        public ReusableSingleButtonBinding(ReusableSingleButtonBindingConfig config) : base(config.ControlPath, config.DisplayName) {
            DebounceThreshold = config.DebounceThreshold ?? DEBOUNCE_THRESHOLD_DEFAULT;
            DebounceMode = config.DebounceMode ?? DEBOUNCE_MODE_DEFAULT;
            PressPoint = config.PressPoint ?? PRESS_POINT_DEFAULT;
            Inverted = config.Inverted ?? INVERTED_DEFAULT;
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
    }
}
