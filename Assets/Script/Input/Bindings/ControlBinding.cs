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
        protected class SingleBinding
        {
            public InputControl<TState> Control;
            public TState State = default;
            public TParams Parameters = new();

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

        protected List<SingleBinding> _bindings = new();

        public ControlBinding(string name, int action) : base(name, action)
        {
        }

        public void AddControl(InputControl<TState> control)
        {
            if (!ContainsControl(control))
                return;

            var binding = new SingleBinding(control);
            _bindings.Add(binding);
            OnControlAdded(binding);
        }

        public void RemoveControl(InputControl<TState> control)
        {
            var binding = FindBinding(control);
            if (binding is null)
                return;

            _bindings.Remove(binding);
        }

        public bool ContainsControl(InputControl<TState> control)
        {
            return FindBinding(control) is not null;
        }

        private SingleBinding FindBinding(InputControl<TState> control)
        {
            foreach (var binding in _bindings)
            {
                if (binding.Control == control)
                    return binding;
            }

            return null;
        }

        protected virtual void OnControlAdded(SingleBinding binding) { }
        protected virtual void OnControlRemoved(SingleBinding binding) { }
    }
}