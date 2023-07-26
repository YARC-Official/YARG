using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using YARG.Core.Input;

namespace YARG.Input
{
    public delegate void GameInputProcessed(ref GameInput input);

    /// <summary>
    /// A binding to one or more controls.
    /// </summary>
    public abstract class ControlBinding
    {
        public interface ISingleBinding
        {
            public InputControl InputControl { get; }
        }

        /// <summary>
        /// Fired when an input event has been processed by this binding.
        /// </summary>
        public event GameInputProcessed InputProcessed;

        /// <summary>
        /// The translation key name for this binding.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// The action enum value for this binding.
        /// </summary>
        public int Action { get; }

        public ControlBinding(string name, int action)
        {
            Name = name;
            Action = action;
        }

        public abstract bool AddControl(InputControl control);
        public abstract bool RemoveControl(InputControl control);
        public abstract bool ContainsControl(InputControl control);
        public abstract IEnumerable<ISingleBinding> AllControls();

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
            try
            {
                InputProcessed?.Invoke(ref input);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Exception when firing input event for {Name}!");
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

        public ControlBinding(string name, int action) : base(name, action)
        {
        }

        public override bool AddControl(InputControl control)
        {
            return control is InputControl<TState> tControl && AddControl(tControl);
        }

        public override bool RemoveControl(InputControl control)
        {
            return control is InputControl<TState> tControl && RemoveControl(tControl);
        }

        public override bool ContainsControl(InputControl control)
        {
            return control is InputControl<TState> tControl && ContainsControl(tControl);
        }

        public override IEnumerable<ISingleBinding> AllControls()
        {
            return Bindings;
        }

        public bool AddControl(InputControl<TState> control)
        {
            if (ContainsControl(control))
                return false;

            var binding = new SingleBinding(control);
            Bindings.Add(binding);
            OnControlAdded(binding);
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

        protected virtual void OnControlAdded(SingleBinding binding) { }
        protected virtual void OnControlRemoved(SingleBinding binding) { }
    }
}