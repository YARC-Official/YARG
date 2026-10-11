using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UIElements;
using YARG.Core.Game;
using YARG.Helpers;
using YARG.Input.Serialization;
using YARG.Menu.ProfileList;
using static YARG.Settings.Preview.FakeTrackPlayer;

namespace YARG.Input.Bindings
{
    public class ReusableButtonBinding : ReusableControlBinding<ReusableSingleButtonBinding, float>
    {
        public bool IsImpulse => Info.Type switch
        {
            BindingType.Button => false,
            BindingType.Impulse => true,
            _ => throw new ArgumentOutOfRangeException($"Unexpected button binding type {Info.Type}")
        };

        private const long DEBOUNCE_THRESHOLD_DEFAULT = 5;

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

        public ReusableButtonBinding(InputActionInfo info) : base(info) {}

        public ReusableButtonBinding(InputActionInfo info, ReusableSingleButtonBindingConfig control)
            : this(info, new List<ReusableSingleButtonBindingConfig>() { control }) { }

        public ReusableButtonBinding(ReusableButtonBinding original) : base(original)
        {
            DebounceThreshold = original.DebounceThreshold;

            foreach (var binding in original.Bindings)
            {
                AddBinding(new(binding));
            }
        }

        public ReusableButtonBinding(InputActionInfo info, List<ReusableSingleButtonBindingConfig> controls) : base(info)
        {
            foreach (var controlConfig in controls)
            {
                AddBinding(new(controlConfig));
            }
        }

        public ReusableButtonBinding(SerializedReusableControlBinding serialized, InputActionInfo info) : base(serialized, info) {
            foreach (var binding in serialized.Controls)
            {
                AddBinding(new(binding));
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
        public string SourceLayout;
        public long? DebounceThreshold;
        public DebounceMode? DebounceMode;
        public float? PressPoint;
        public bool? Inverted;

        public ReusableSingleButtonBindingConfig(ControllerFamily family, string controlPath)
        {
            ControlItemInfo control = LayoutHelper.GetControlInfo(family, controlPath);

            ControlPath = control.ControlPath;
            DisplayName = control.DisplayName;
            SourceLayout = LayoutHelper.ControllerFamilyToLayoutString(family);

            // These would be more pleasant as struct field initializers, but those aren't in C# 9.0
            DebounceThreshold = null;
            DebounceMode = null;
            PressPoint = null;
            Inverted = null;
        }
    }
}
