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
        public bool Inverted { get; }
        public DebounceMode DebounceMode { get; set; }
        public float PressPoint { get; }
        private DebounceTimer<float> _debounceTimer;
        public bool IsPressed => State >= PressPoint;
        public bool WasPreviouslyPressed => PreviousState >= PressPoint;
        public float PreviousState { get; private set; }

        private float _invertSign => Inverted ? -1 : 1;



        public RuntimeSingleButtonBinding(InputControl<float> control, ReusableSingleButtonBinding reusableBinding) : base(control)
        {
            _debounceTimer = new()
            {
                TimeThreshold = reusableBinding.DebounceThreshold,
            };

            Inverted = reusableBinding.Inverted;
            DebounceMode = reusableBinding.DebounceMode;
            PressPoint = reusableBinding.PressPoint;
        }

        public void UpdateDebounce(double time)
        {
            if (!_debounceTimer.IsRunning || !_debounceTimer.HasElapsed(time))
                return;

            State = _debounceTimer.Stop();
            InvokeStateChanged(State);
            return;
        }

        public override void UpdateState(double time)
        {
            PreviousState = State;

            // Read new state
            _debounceTimer.UpdateValue(Control.value * _invertSign);

            // Wait for debounce to end
            if (!_debounceTimer.HasElapsed(time))
                return;

            State = _debounceTimer.Stop();

            if (DebounceMode == DebounceMode.PressAndRelease ||
                (IsPressed && DebounceMode == DebounceMode.Press) ||
                (!IsPressed && DebounceMode == DebounceMode.Release))
            {
                _debounceTimer.Start(time);
            }

            InvokeStateChanged(State);
        }
    }
}
