using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

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
            PressPoint = reusableBinding.HasPressPointOverride
                ? reusableBinding.PressPoint
                : control is ButtonControl button
                    ? button.pressPointOrDefault
                    : InputSystem.settings.defaultButtonPressPoint;
        }

        public void UpdateDebounce(double time)
        {
            if (!_debounceTimer.IsRunning || !_debounceTimer.HasElapsed(time))
            {
                JustPressed = false;
                return;
            }

            SetState(_debounceTimer.Stop());
        }

        public override void UpdateState(double time, float value)
        {
            _debounceTimer.UpdateValue(value * _invertSign);

            if (!_debounceTimer.HasElapsed(time))
            {
                JustPressed = false;
                return;
            }

            bool wasPressed = IsPressed;
            SetState(_debounceTimer.Stop());

            // Only arm the debounce timer for a real pressed-state transition, so a held
            // control cannot keep re-arming the window and swallow subsequent presses.
            if (IsPressed != wasPressed &&
                (DebounceMode == DebounceMode.PressAndRelease ||
                 (IsPressed && DebounceMode == DebounceMode.Press) ||
                 (!IsPressed && DebounceMode == DebounceMode.Release)))
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
