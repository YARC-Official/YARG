using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.InputSystem;
using YARG.Input.Bindings;

namespace YARG.Input
{
    public class RuntimeSingleBinding<TState> where TState: struct
    {
        public TState State { get; protected set; }
        public event Action<TState> StateChanged;
        public InputControl<TState> Control { get; }

        public RuntimeSingleBinding(ReusableSingleBinding reusableBinding)
        {
            // TODO-FRICK: I think this is where the actual wiring up happens
        }

        public virtual void UpdateState(double time)
        {
            State = Control.value;
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
