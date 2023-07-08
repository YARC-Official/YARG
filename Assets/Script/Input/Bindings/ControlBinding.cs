using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using YARG.Core.Input;

namespace YARG.Input
{
    /// <summary>
    /// A binding to one or more controls.
    /// </summary>
    public abstract class ControlBinding
    {
        public event Action<GameInput> InputProcessed;

        public string DisplayName { get; }

        protected readonly int _action;

        public ControlBinding(string displayName, int action)
        {
            DisplayName = displayName;
            _action = action;
        }

        public virtual void UpdateForFrame() { }
        public abstract void ProcessInputEvent(InputEventPtr eventPtr);

        protected void FireEvent(double time, int value)
        {
            time = InputManager.GetRelativeTime(time);
            var input = new GameInput(time, _action, value);
            InputProcessed?.Invoke(input);
        }

        protected void FireEvent(double time, float value)
        {
            time = InputManager.GetRelativeTime(time);
            var input = new GameInput(time, _action, value);
            InputProcessed?.Invoke(input);
        }

        protected void FireEvent(double time, bool value)
        {
            time = InputManager.GetRelativeTime(time);
            var input = new GameInput(time, _action, value);
            InputProcessed?.Invoke(input);
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

        public ControlBinding(string displayName, int action) : base(displayName, action)
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

        protected virtual void OnControlAdded(SingleBinding binding)
        {
        }

        protected virtual void OnControlRemoved(SingleBinding binding)
        {
        }
    }
}