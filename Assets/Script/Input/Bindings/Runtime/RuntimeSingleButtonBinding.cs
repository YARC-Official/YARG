using System;
using System.Collections.Generic;
using System.Text;
using UnityEditorInternal;
using UnityEngine.InputSystem;
using YARG.Input;
using YARG.Input.Bindings;

namespace YARG.Input
{
    public class RuntimeSingleButtonBinding : RuntimeSingleBinding<float>
    {
        public float PressPoint { get; }
        public bool IsPressed => State >= PressPoint;

        private DebounceTimer<float> _debounceTimer;

        public RuntimeSingleButtonBinding(InputControl<float> control, ReusableSingleButtonBinding reusableBinding) : base(control)
        {
            _debounceTimer = new()
            {
                TimeThreshold = reusableBinding.DebounceThreshold,
            };
        }

        public void UpdateDebounce(double time)
        {
            if (!_debounceTimer.IsRunning || !_debounceTimer.HasElapsed(time))
                return;

            State = _debounceTimer.Stop();
            InvokeStateChanged(State);
            return;
        }
    }
}
