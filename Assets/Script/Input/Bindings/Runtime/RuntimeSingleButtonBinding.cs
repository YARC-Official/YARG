using UnityEngine.InputSystem;

namespace YARG.Input.Bindings
{
    public class RuntimeSingleButtonBinding : RuntimeSingleBinding<float>
    {
        public bool Inverted { get; }
        public DebounceMode DebounceMode { get; set; }
        public float PressPoint { get; }
        private DebounceTimer<float> _debounceTimer;
        public bool IsPressed => State >= PressPoint;
        public bool JustPressed { get; private set; }
        public float PreviousState { get; private set; }

        private float _invertSign => Inverted ? -1 : 1;

        public RuntimeSingleButtonBinding(
        InputControl<float> control,
        ReusableSingleButtonBinding reusableBinding)
        : base(control)
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

            SetState(_debounceTimer.Stop());

            if (DebounceMode == DebounceMode.PressAndRelease ||
                (IsPressed && DebounceMode == DebounceMode.Press) ||
                (!IsPressed && DebounceMode == DebounceMode.Release))
            {
                _debounceTimer.Start(time);
            }
        }

        public override void UpdateState(double time)
        {
            _debounceTimer.UpdateValue(Control.value * _invertSign);

            if (!_debounceTimer.HasElapsed(time))
                return;

            SetState(_debounceTimer.Stop());

            if (DebounceMode == DebounceMode.PressAndRelease ||
                (IsPressed && DebounceMode == DebounceMode.Press) ||
                (!IsPressed && DebounceMode == DebounceMode.Release))
            {
                _debounceTimer.Start(time);
            }
        }

        private void SetState(float newState)
        {
            bool wasPressed = IsPressed;

            State = newState;

            JustPressed = !wasPressed && IsPressed;

            InvokeStateChanged(State);
        }
    }
}
