using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

namespace YARG.Input {
    public enum BindingType {
        BUTTON,
        AXIS
    }

    public class ControlBinding {
		public const float DEFAULT_PRESS_THRESHOLD = 0.75f; // TODO: Remove once control calibration is added

        public BindingType Type { get; }
        public string DisplayName { get; }
        public string BindingKey { get; }

        private InputControl<float> _control;
        public InputControl<float> Control {
            get => _control;
            set {
                _control = value;
                if (value is ButtonControl button) {
                    pressPoint = button.pressPointOrDefault;
                }
            }
        }

        private (float previous, float current) _state;
        public (float previous, float current) State => _state;

        private float pressPoint = DEFAULT_PRESS_THRESHOLD;

        public ControlBinding(BindingType type, string displayName, string bindingKey) {
            Type = type;
            DisplayName = displayName;
            BindingKey = bindingKey;
        }

        public bool IsPressed() {
            // Ignore if unmapped
            if (_control == null) {
                return false;
            }

            return _state.current >= pressPoint;
        }

        public bool WasPressed() {
            // Ignore if unmapped
            if (_control == null) {
                return false;
            }

            return _state.previous < pressPoint && _state.current >= pressPoint;
        }

        public bool WasReleased() {
            // Ignore if unmapped
            if (_control == null) {
                return false;
            }

            return _state.previous >= pressPoint && _state.current < pressPoint;
        }

        public void UpdateState(InputEventPtr eventPtr) {
            // Ignore if unmapped
            if (_control == null) {
                return;
            }

            // Progress state history forward
            _state.previous = _state.current;
            // Don't check pressed state unless there was a value change
            // Controls not changed in a delta state event (which MIDI devices use) will report the wrong value
            if (_control.HasValueChangeInEvent(eventPtr)) {
                _state.current = _control.ReadValueFromEvent(eventPtr);
            }
        }
    }
}