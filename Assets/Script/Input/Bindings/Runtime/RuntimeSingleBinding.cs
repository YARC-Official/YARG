using System;
using UnityEngine.InputSystem;

namespace YARG.Input.Bindings
{
    public class RuntimeSingleBinding<TState> where TState: struct
    {
        public TState State { get; protected set; }
        public event Action<TState> StateChanged;
        public InputControl<TState> Control { get; }

        public RuntimeSingleBinding(InputControl<TState> control)
        {
            Control = control;
        }

        public virtual void UpdateState(double time, TState value)
        {
            State = value;
            InvokeStateChanged(State);
        }

        public virtual void ResetState()
        {
            State = default;
            InvokeStateChanged(State);
        }

        protected void InvokeStateChanged(TState state)
        {
            StateChanged?.Invoke(state);
        }
    }
}
