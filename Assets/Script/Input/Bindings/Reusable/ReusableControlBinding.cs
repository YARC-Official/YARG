using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine.InputSystem;
using YARG.Core.Logging;
using YARG.Input.Serialization;
using YARG.Localization;

namespace YARG.Input
{
    public abstract class ReusableControlBinding : IControlBinding {
        public Dictionary<string, string> Parameters;
        public abstract BindingType Type { get; }
        public InputControl Control { get; }
        public string Name { get; }
        public string NameLefty { get; }
        public event Action BindingsChanged;
        public abstract SerializedReusableControlBinding Serialize();

        public ReusableControlBinding(string name)
        {
            Name = Localize.Key("Bindings", name);
            // TODO-FRICK - NameLefty
        }
    }

    public abstract class ReusableControlBinding<TState, TSingle> : ReusableControlBinding, IControlBinding<TState, TSingle>
        where TState : struct
        where TSingle : ISingleBinding<TState>
    {
        public List<ReusableSingleBinding<TState>> Bindings;
        public TState State { get; set; }
        public event Action StateChanged;

        public ReusableControlBinding(string name, SerializedReusableControlBinding? serialized = null) : base(name)
        {
            Parameters = serialized?.Parameters ?? new();
        }

        public override SerializedReusableControlBinding Serialize()
        {
            var controls = new List<SerializedInputControl>();

            foreach (var binding in Bindings)
            {
                controls.Add(binding.Serialize());
            }

            return new()
            {
                Parameters = Parameters ?? new(),
                Controls = controls
            };
        }

        public bool RemoveBinding(TSingle single)
        {
            var removed = false;

            var newControls = new List<ReusableSingleBinding<TState>>();

            foreach (var control in Bindings)
            {
                if (control.ControlName == single.ControlName)
                {
                    removed = true;
                }
                else
                {
                    newControls.Add(control);
                }
            }

            if (removed)
            {
                Bindings = newControls;
            }

            return removed;
        }
    }

    public class ReusableButtonBinding : ReusableControlBinding<float, ReusableSingleButtonBinding>
    {
        private const long DEBOUNCE_DEFAULT = 5;
        private const string DEBOUNCE_THRESHOLD = "DebounceThreshold";

        public override BindingType Type => BindingType.Button;

        public long DebounceThreshold = DEBOUNCE_DEFAULT;

        public ReusableButtonBinding(string name, SerializedReusableControlBinding? serialized = null) : base(name, serialized)
        {
            foreach (var control in serialized.Controls)
            {
                Bindings.Add(new ReusableSingleButtonBinding(control));
            }

            if (serialized.Parameters.TryGetValue(DEBOUNCE_THRESHOLD, out var debounceString))
            {
                long.TryParse(debounceString, out DebounceThreshold);
            }
        }
    }

    public class ReusableAxisBinding : ReusableControlBinding<float, ReusableSingleAxisBinding>
    {
        public override BindingType Type => BindingType.Axis;

        public ReusableAxisBinding(string name, SerializedReusableControlBinding? serialized = null) : base(name, serialized)
        {
            // TODO-FRICK: Axis parameters
        }
    }

    public class ReusableIntegerBinding : ReusableControlBinding<int, ReusableSingleIntegerBinding>
    {
        public override BindingType Type => BindingType.Integer;

        public ReusableIntegerBinding(string name, SerializedReusableControlBinding? serialized = null) : base(name, serialized)
        {
            // TODO-FRICK: Integer parameters
        }
    }

    public interface IControlBinding {
        public event Action BindingsChanged;
        public string Name { get; }
        public string NameLefty { get; }
    }

    public interface IControlBinding<TState, TBinding> : IControlBinding
        where TState : struct
        where TBinding : ISingleBinding<TState>
    {
        bool RemoveBinding(TBinding single);
        public TState State { get; set; }
        public event Action StateChanged;
    }

    public interface IButtonBinding : IControlBinding<float, ISingleButtonBinding> { }
    public interface IAxisBinding : IControlBinding<float, ISingleAxisBinding> { }
    public interface IIntegerBinding : IControlBinding<int, ISingleIntegerBinding> { }

    public interface ISingleBinding<TState>
        where TState : struct
    {
        string ControlName { get; }
        public TState State { get; set; }
        public event Action<TState> StateChanged;
    }

    public interface ISingleButtonBinding : ISingleBinding<float> {
        public bool Inverted { get; set; }
        public float PressPoint { get; set; }
        public DebounceMode DebounceMode { get; set; }
        public long DebounceThreshold { get; set; }
    }
    public interface ISingleAxisBinding : ISingleBinding<float> {
        public bool Inverted { get; set; }
        public float Maximum { get; set; }
        public float Minimum { get; set; }
        public float UpperDeadzone { get; set; }
        public float LowerDeadzone { get; set; }
    }
    public interface ISingleIntegerBinding : ISingleBinding<int> {

    }

    public abstract class ReusableSingleBinding<TState> : ISingleBinding<TState>
        where TState: struct
    {
        private Dictionary<string, string> _parameters = new();
        public TState State { get; set; }
        public event Action<TState> StateChanged;

        public ReusableSingleBinding(string controlName)
        {
            ControlName = controlName;
        }

        public ReusableSingleBinding(SerializedInputControl serialized)
        {
            ControlName = serialized.ControlName;
            _parameters = serialized.Parameters;
        }

        public SerializedInputControl Serialize()
        {
            return new SerializedInputControl(ControlName)
            {
                Parameters = _parameters
            };
        }

        public string ControlName { get; }
    }

    public class ReusableSingleButtonBinding : ReusableSingleBinding<float>, ISingleButtonBinding
    {
        private const string INVERTED = "Inverted";
        private const string PRESS_POINT = "PressPoint";

        public bool Inverted { get; set; } = false;
        public float PressPoint { get; set; } = 0.5f;
        public DebounceMode DebounceMode { get; set; }
        public long DebounceThreshold { get; set; }

        public ReusableSingleButtonBinding(string controlName) : base(controlName) {}

        public ReusableSingleButtonBinding(SerializedInputControl serialized) : base(serialized) {
            foreach (var (key, val) in serialized.Parameters)
            {
                switch (key)
                {
                    case INVERTED:
                        if (bool.TryParse(val, out var inverted))
                        {
                            Inverted = inverted;
                        }
                        break;

                    case PRESS_POINT:
                        if (float.TryParse(val, out var pressPoint))
                        {
                            PressPoint = pressPoint;
                        }
                        break;
                    default:
                        YargLogger.LogWarning($"Unknown button binding parameter {key}; skipping");
                        break;
                }
            }
        }
    }

    public class ReusableSingleAxisBinding : ReusableSingleBinding<float>, ISingleAxisBinding
    {
        public const string INVERTED = "Inverted";
        public const string MAXIMUM = "Maximum";
        public const string MINIMUM = "Minimum";
        public const string UPPER_DEADZONE = "UpperDeadzone";
        public const string LOWER_DEADZONE = "LowerDeadzone";


        public bool Inverted { get; set; }
        public float Maximum { get; set; }
        public float Minimum { get; set; }
        public float UpperDeadzone { get; set; }
        public float LowerDeadzone { get; set; }

        public ReusableSingleAxisBinding(string controlName) : base(controlName) { }

        public ReusableSingleAxisBinding(SerializedInputControl serialized) : base(serialized) {
            foreach (var (key, val) in serialized.Parameters)
            {
                switch (key)
                {
                    case INVERTED:
                        if (bool.TryParse(val, out var inverted))
                        {
                            Inverted = inverted;
                        }
                        break;

                    case MAXIMUM:
                        if (float.TryParse(val, out var maximum))
                        {
                            Maximum = maximum;
                        }
                        break;
                    case MINIMUM:
                        if (float.TryParse(val, out var minimum))
                        {
                            Minimum = minimum;
                        }
                        break;
                    case UPPER_DEADZONE:
                        if (float.TryParse(val, out var upperDeadzone))
                        {
                            UpperDeadzone = upperDeadzone;
                        }
                        break;
                    case LOWER_DEADZONE:
                        if (float.TryParse(val, out var lowerDeadzone))
                        {
                            LowerDeadzone = lowerDeadzone;
                        }
                        break;
                    default:
                        YargLogger.LogWarning($"Unknown button binding parameter {key}; skipping");
                        break;
                }
            }
        }
    }

    public class ReusableSingleIntegerBinding : ReusableSingleBinding<int>, ISingleIntegerBinding
    {
        public ReusableSingleIntegerBinding(string controlName) : base(controlName) { }

        public ReusableSingleIntegerBinding(SerializedInputControl serialized) : base(serialized) {
            // TODO-FRICK: Params
        }
    }
}
