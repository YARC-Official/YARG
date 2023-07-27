using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Localization;
using YARG.Core.Input;

namespace YARG.Input
{
    public delegate void GameInputProcessed(ref GameInput input);

    public interface ISingleBinding
    {
        public InputControl InputControl { get; }
    }

    public class ActuationSettings
    {
        public float ButtonPressThreshold = 0.5f;
        public float AxisDeltaThreshold = 0.05f;
        public int IntegerDeltaThreshold = 1;
    }

    /// <summary>
    /// A binding to one or more controls.
    /// </summary>
    public abstract class ControlBinding
    {
        /// <summary>
        /// Fired when an input event has been processed by this binding.
        /// </summary>
        public event GameInputProcessed InputProcessed;

        /// <summary>
        /// The controls bound to this binding.
        /// </summary>
        public abstract IEnumerable<ISingleBinding> Controls { get; }

        /// <summary>
        /// The type used for this binding.
        /// </summary>
        public abstract Type ControlType { get; }

        /// <summary>
        /// The name for this binding.
        /// </summary>
        public LocalizedString Name { get; }

        /// <summary>
        /// The key string for this binding.
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// The action enum value for this binding.
        /// </summary>
        public int Action { get; }

        /// <summary>
        /// Whether or not this control is enabled.
        /// </summary>
        public bool Enabled { get; protected set; } = false;

        public ControlBinding(string name, int action)
        {
            Key = name;
            Name = new("Bindings", name);
            Action = action;
        }

        public abstract bool IsControlCompatible(InputControl control);
        public abstract bool IsControlActuated(ActuationSettings settings, InputControl control, InputEventPtr eventPtr);

        public abstract bool AddControl(ActuationSettings settings, InputControl control);
        public abstract bool RemoveControl(InputControl control);
        public abstract bool ContainsControl(InputControl control);

        public virtual void Enable()
        {
            Enabled = true;
        }

        public virtual void Disable()
        {
            Enabled = false;
        }

        public virtual void UpdateForFrame() { }
        public abstract void ProcessInputEvent(InputEventPtr eventPtr);

        protected void FireEvent(double time, int value)
        {
            time = InputManager.GetRelativeTime(time);
            var input = new GameInput(time, Action, value);
            FireEvent(ref input);
        }

        protected void FireEvent(double time, float value)
        {
            time = InputManager.GetRelativeTime(time);
            var input = new GameInput(time, Action, value);
            FireEvent(ref input);
        }

        protected void FireEvent(double time, bool value)
        {
            time = InputManager.GetRelativeTime(time);
            var input = new GameInput(time, Action, value);
            FireEvent(ref input);
        }

        protected void FireEvent(ref GameInput input)
        {
            if (!Enabled)
                return;

            try
            {
                InputProcessed?.Invoke(ref input);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Exception when firing input event for {Key}!");
                Debug.LogException(ex);
            }
        }
    }

    /// <summary>
    /// A binding to one or more controls.
    /// </summary>
    public abstract class ControlBinding<TState, TParams> : ControlBinding
        where TState : struct
        where TParams : new()
    {
        protected class SingleBinding : ISingleBinding
        {
            public InputControl<TState> Control;
            public TState State;
            public TParams Parameters = new();

            public InputControl InputControl => Control;

            public SingleBinding(InputControl<TState> control)
            {
                Control = control;
            }

            public TState UpdateState(InputEventPtr eventPtr)
            {
                if (Control.HasValueChangeInEvent(eventPtr))
                {
                    State = Control.ReadValueFromEvent(eventPtr);
                }

                return State;
            }
        }

        protected List<SingleBinding> Bindings = new();
        public override IEnumerable<ISingleBinding> Controls => Bindings;

        public override Type ControlType => typeof(TState);

        public ControlBinding(string name, int action) : base(name, action)
        {
        }

        public override bool IsControlCompatible(InputControl control)
        {
            return IsControlCompatible(control, out _);
        }

        public virtual bool IsControlCompatible(InputControl control, out InputControl<TState> typedControl)
        {
            if (control is InputControl<TState> tControl)
            {
                typedControl = tControl;
                return true;
            }

            typedControl = null;
            return false;
        }

        public override bool IsControlActuated(ActuationSettings settings, InputControl control, InputEventPtr eventPtr)
        {
            return IsControlCompatible(control, out var tControl) && IsControlActuated(settings, tControl, eventPtr);
        }

        public abstract bool IsControlActuated(ActuationSettings settings, InputControl<TState> control, InputEventPtr eventPtr);

        public override bool AddControl(ActuationSettings settings, InputControl control)
        {
            return IsControlCompatible(control, out var tControl) && AddControl(settings, tControl);
        }

        public override bool RemoveControl(InputControl control)
        {
            return IsControlCompatible(control, out var tControl) && RemoveControl(tControl);
        }

        public override bool ContainsControl(InputControl control)
        {
            return IsControlCompatible(control, out var tControl) && ContainsControl(tControl);
        }

        public bool AddControl(ActuationSettings settings, InputControl<TState> control)
        {
            if (ContainsControl(control))
                return false;

            var binding = new SingleBinding(control);
            Bindings.Add(binding);
            OnControlAdded(settings, binding);
            return true;
        }

        public bool RemoveControl(InputControl<TState> control)
        {
            if (!TryFindBinding(control, out var binding))
                return false;

            bool removed = Bindings.Remove(binding);
            if (removed)
                OnControlRemoved(binding);

            return removed;
        }

        public bool ContainsControl(InputControl<TState> control)
        {
            return TryFindBinding(control, out _);
        }

        private bool TryFindBinding(InputControl<TState> control, out SingleBinding foundBinding)
        {
            foreach (var binding in Bindings)
            {
                if (binding.Control == control)
                {
                    foundBinding = binding;
                    return true;
                }
            }

            foundBinding = null;
            return false;
        }

        protected virtual void OnControlAdded(ActuationSettings settings, SingleBinding binding) { }
        protected virtual void OnControlRemoved(SingleBinding binding) { }
    }
}