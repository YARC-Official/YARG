using System.Collections.Generic;
using YARG.Input.Serialization;
using UnityEngine.InputSystem;
using System.Linq;

namespace YARG.Input
{
    public abstract class ReusableControlBinding {
        public Dictionary<string, string> Parameters;
        public abstract BindingType Type { get; }
        public InputControl Control { get; }
        public abstract SerializedReusableControlBinding Serialize();
    }

    public abstract class ReusableControlBinding<TState, TSingle> : ReusableControlBinding, IControlBinding<TState, TSingle>
        where TState : struct
        where TSingle : ISingleBinding<TState>
    {
        public List<ReusableSingleBinding<TState>> Bindings;

        public ReusableControlBinding(SerializedReusableControlBinding? serialized = null)
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

        public ReusableButtonBinding(SerializedReusableControlBinding? serialized = null) : base(serialized)
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

        public ReusableAxisBinding(SerializedReusableControlBinding? serialized = null) : base(serialized)
        {
            // TODO-FRICK: Axis parameters
        }
    }

    public class ReusableIntegerBinding : ReusableControlBinding<int, ReusableSingleIntegerBinding>
    {
        public override BindingType Type => BindingType.Integer;

        public ReusableIntegerBinding(SerializedReusableControlBinding? serialized = null) : base(serialized)
        {
            // TODO-FRICK: Integer parameters
        }
    }

    public interface IControlBinding<TState, TBinding>
        where TState : struct
        where TBinding : ISingleBinding<TState>
    {
        bool RemoveBinding(TBinding single);
    }

    public interface IButtonBinding : IControlBinding<float, ISingleButtonBinding> { }
    public interface IAxisBinding : IControlBinding<float, ISingleAxisBinding> { }
    public interface IIntegerBinding : IControlBinding<int, ISingleIntegerBinding> { }

    public interface ISingleBinding<TState>
        where TState : struct
    {
        string ControlName { get; }
    }

    public interface ISingleButtonBinding : ISingleBinding<float> { }
    public interface ISingleAxisBinding : ISingleBinding<float> { }
    public interface ISingleIntegerBinding : ISingleBinding<int> { }

    public abstract class ReusableSingleBinding<TState> : ISingleBinding<TState>
        where TState: struct
    {
        private Dictionary<string, string> _parameters = new();

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

    public class ReusableSingleButtonBinding : ReusableSingleBinding<float>
    {
        public ReusableSingleButtonBinding(SerializedInputControl serialized) : base(serialized) {
            // TODO-FRICK: Params
        }
    }

    public class ReusableSingleAxisBinding : ReusableSingleBinding<float>
    {
        public ReusableSingleAxisBinding(SerializedInputControl serialized) : base(serialized) {
            // TODO-FRICK: Params
        }
    }

        public class ReusableSingleIntegerBinding : ReusableSingleBinding<int>
    {
        public ReusableSingleIntegerBinding(SerializedInputControl serialized) : base(serialized) {
            // TODO-FRICK: Params
        }
    }
}
