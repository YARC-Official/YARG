using System.Diagnostics;
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

        /// <summary>
        /// The minimum number of milliseconds for the debounce threshold.
        /// </summary>
        public const long DEBOUNCE_MINIMUM = 1;

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

        private (float previous, float current, float postDebounce) _state;
        public (float previous, float current) State => (_state.previous, _state.current);

        private float pressPoint = DEFAULT_PRESS_THRESHOLD;

        private Stopwatch debounceTimer = new();

        private long _debounceThreshold = 0;
        /// <summary>
        /// The debounce time threshold, in milliseconds. Use 0 or less to disable debounce.
        /// </summary>
        public long DebounceThreshold {
            get => _debounceThreshold;
            set {
                // Limit debounce amount to 0-100 ms
                // 100 ms is *very* generous, any larger and input registration will be very bad
                // If someone needs a larger amount their controller is just busted lol
                if (value > 100) {
                    value = 100;
                } else if (value < 0) {
                    value = 0;
                }

                _debounceThreshold = value;
            }
        }

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
            float value = _state.current;
            _state.previous = value;
            // Don't read new value unless there was a value change
            // Controls not changed in a delta state event (which MIDI devices use) will report the wrong value
            if (_control.HasValueChangeInEvent(eventPtr)) {
                value = _control.ReadValueFromEvent(eventPtr);
            }

            // Store value
            _state.postDebounce = value;
            if (!debounceTimer.IsRunning) {
                _state.current = value;

                // Start debounce timer if the current state has changed
                if (DebounceThreshold >= DEBOUNCE_MINIMUM && Type == BindingType.BUTTON && _state.current != _state.previous) {
                    debounceTimer.Start();
                }
            }

            UpdateDebounce();
        }

        /// <summary>
        /// Updates the debounce state of this binding.
        /// </summary>
        /// <returns>
        /// True if debouncing has finished and the binding state changed, false otherwise.
        /// </returns>
        public bool UpdateDebounce() {
            // Ignore if not a button, or threshold is below the minimum
            if (Type != BindingType.BUTTON || DebounceThreshold < DEBOUNCE_MINIMUM) {
                if (debounceTimer.IsRunning) {
                    debounceTimer.Reset();
                }
                return false;
            }

            // Check time elapsed
            if (debounceTimer.ElapsedMilliseconds >= DebounceThreshold) {
                // Stop timer and progress state history forward
                debounceTimer.Reset();
                _state.previous = _state.current;
                _state.current = _state.postDebounce;
                return true;
            }

            return false;
        }
    }
}