using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.InputSystem;
using YARG.Core.Logging;
using YARG.Helpers;
using YARG.Input.Serialization;

namespace YARG.Input.Bindings
{
    public abstract class ReusableSingleBinding
    {
        public event Action Changed;
        protected void NotifyChanged()
        {
            Changed?.Invoke();
        }

        private string _controlPath;
        public string ControlPath {
            get => _controlPath;
            set
            {
                if (!string.Equals(_controlPath, value, StringComparison.OrdinalIgnoreCase))
                {
                    _controlPath = value;
                    Changed?.Invoke();
                }
            }
        }

        public string DisplayName { get; set; }

        private string _sourceLayout;
        public string SourceLayout
        {
            get => _sourceLayout;
            set
            {
                if (!string.Equals(_sourceLayout, value, StringComparison.OrdinalIgnoreCase))
                {
                    _sourceLayout = value;
                    Changed?.Invoke();
                }
            }
        }

        public ReusableSingleBinding(string controlPath, string displayName, string sourceLayout)
        {
            ControlPath = controlPath;
            DisplayName = displayName;
            SourceLayout = sourceLayout;
        }

        public ReusableSingleBinding(SerializedSingleBinding serialized) : this(
            serialized.ControlName,
            LayoutHelper.GetControlDisplayName(serialized.SourceLayout, serialized.ControlName),
            serialized.SourceLayout
        )
        { }

        public SerializedSingleBinding Serialize()
        {
            return new SerializedSingleBinding(ControlPath, SourceLayout)
            {
                Parameters = SerializeParameters()
            };
        }

        protected virtual Dictionary<string, string> SerializeParameters()
        {
            return new();
        }

        // Override this for single binding types that have parameters to parse
        protected virtual void DeserializeParameters(Dictionary<string, string> parameters)
        {
            foreach (var (key, val) in parameters)
            {
                ReusableControlBinding.LogUnknownParameter(key, val);
            }
        }

        public abstract bool IsControlBeingQuickBound(InputControl control);
    }

    public abstract class ReusableSingleBinding<TState> : ReusableSingleBinding
        where TState : struct
    {
        public ReusableSingleBinding(ReusableSingleBinding<TState> original) : base(original.ControlPath, original.DisplayName, original.SourceLayout) { }
        public ReusableSingleBinding(string controlPath, string displayName, string sourceLayout) : base(controlPath, displayName, sourceLayout) { }
        public ReusableSingleBinding(SerializedSingleBinding serialized) : base(serialized) { }

        public RuntimeSingleBinding<TState> MakeRuntime(InputDevice controller)
        {
            var control = InputControlPath.TryFindControl(controller, $"*/{ControlPath}");

            if (control is null)
            {
                // TODO-FRICK: The old bindings fallback from ControlBinding::DeserializeControl? Are we sanitizing that away now?

                YargLogger.LogWarning($"Could not find control {ControlPath} on controller {controller}!");
                return null;
            }

            if (control is not InputControl<TState> tControl)
            {
                YargLogger.LogWarning(
                    $"Found control {ControlPath}, but it was not of the right type!" +
                    $"Expected a derivative of {typeof(InputControl<TState>)}, found {control.GetType()}"
                );
                return null;
            }

            return MakeRuntime(tControl);
        }

        protected abstract RuntimeSingleBinding<TState> MakeRuntime(InputControl<TState> control);

        public override bool IsControlBeingQuickBound(InputControl control)
        {
            return IsControlCompatible(control, out var typedControl) && IsControlActuated(typedControl);
        }

        protected virtual bool IsControlCompatible(InputControl control, out InputControl<TState> typedControl)
        {
            if (control is InputControl<TState> tControl)
            {
                typedControl = tControl;
                return true;
            }

            typedControl = null;
            return false;
        }

        protected abstract bool IsControlActuated(InputControl<TState> typedControl);
    }
}
