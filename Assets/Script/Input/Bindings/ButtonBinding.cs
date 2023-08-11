using System;
using System.Diagnostics;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using YARG.Input.Serialization;

namespace YARG.Input
{
    public class SingleButtonBinding : SingleBinding<float>
    {
        public const long DEBOUNCE_TIME_MAX = 25;
        public const long DEBOUNCE_ACTIVE_THRESHOLD = 1;

        private Stopwatch _debounceTimer = new();
        private long _debounceThreshold = 0;

        /// <summary>
        /// The debounce time threshold, in milliseconds. Use 0 or less to disable debounce.
        /// </summary>
        public long DebounceThreshold
        {
            get => _debounceThreshold;
            // Limit debounce amount to 0-25 ms
            // Any larger and input registration will be very bad, the max will limit to 40 inputs per second
            // If someone needs a larger amount their controller is just busted lol
            set => _debounceThreshold = Math.Clamp(value, 0, DEBOUNCE_TIME_MAX);
        }

        public bool DebounceEnabled => DebounceThreshold >= DEBOUNCE_ACTIVE_THRESHOLD;

        public float PressPoint;

        private float _postDebounceValue;

        public SingleButtonBinding(InputControl<float> control) : base(control)
        {
        }

        public SingleButtonBinding(InputControl<float> control, ActuationSettings settings)
            : base(control)
        {
            float pressPoint = settings.ButtonPressThreshold;
            if (control is ButtonControl button)
            {
                pressPoint = button.pressPointOrDefault;
            }

            PressPoint = pressPoint;
        }

        public SingleButtonBinding(InputControl<float> control, SerializedInputControl serialized)
            : base(control, serialized)
        {
            if (!serialized.Parameters.TryGetValue(nameof(PressPoint), out string pressPointText) ||
                !float.TryParse(pressPointText, out float pressPoint))
                pressPoint = InputSystem.settings.defaultButtonPressPoint;

            PressPoint = pressPoint;

            if (!serialized.Parameters.TryGetValue(nameof(DebounceThreshold), out string debounceText) ||
                !long.TryParse(debounceText, out long debounceThreshold))
                debounceThreshold = 0;

            DebounceThreshold = debounceThreshold;
        }

        public override SerializedInputControl Serialize()
        {
            var serialized = base.Serialize();
            if (serialized is null)
                return null;

            serialized.Parameters.Add(nameof(PressPoint), PressPoint.ToString());
            serialized.Parameters.Add(nameof(DebounceThreshold), DebounceThreshold.ToString());

            return serialized;
        }

        public override float UpdateState(InputEventPtr eventPtr)
        {
            // Ignore if no state change occured
            if (!Control.HasValueChangeInEvent(eventPtr))
                return State;

            // Read new state
            float current = Control.ReadValueFromEvent(eventPtr);

            // Check debounce
            if (_debounceTimer.IsRunning && _debounceTimer.ElapsedMilliseconds < DebounceThreshold)
            {
                // Save for when debounce ends
                _postDebounceValue = current;
                return State;
            }

            // Stop debounce and process this event normally
            _debounceTimer.Reset();
            _postDebounceValue = State = current;

            // Start debounce again if enabled
            if (DebounceEnabled)
                _debounceTimer.Start();

            return current;
        }

        public bool UpdateDebounce(out float postDebounce)
        {
            postDebounce = -1;

            // Ignore if debounce is disabled
            if (!DebounceEnabled)
                return false;

            // Check time elapsed
            if (_debounceTimer.ElapsedMilliseconds >= DebounceThreshold)
            {
                // Stop timer and process post-debounce value
                _debounceTimer.Reset();
                postDebounce = State = _postDebounceValue;
                return true;
            }

            return false;
        }
    }

    public class ButtonBinding : ControlBinding<float, SingleButtonBinding>
    {
        private bool _currentValue;

        public ButtonBinding(string name, int action) : base(name, action)
        {
        }

        public override bool IsControlActuated(ActuationSettings settings, InputControl<float> control,
            InputEventPtr eventPtr)
        {
            if (!control.HasValueChangeInEvent(eventPtr))
                return false;

            float pressPoint = settings.ButtonPressThreshold;
            float value = control.ReadValueFromEvent(eventPtr);

            return value >= pressPoint;
        }

        public override void ProcessInputEvent(InputEventPtr eventPtr)
        {
            bool state = false;
            foreach (var binding in _bindings)
            {
                var value = binding.UpdateState(eventPtr);
                state |= value >= binding.PressPoint;
            }

            ProcessNextState(eventPtr.time, state);
        }

        private void ProcessNextState(double time, bool state)
        {
            // Ignore if state is unchanged
            if (_currentValue == state)
                return;

            _currentValue = state;
            FireInputEvent(time, state);
        }

        public override void UpdateForFrame()
        {
            UpdateDebounce();
        }

        private void UpdateDebounce()
        {
            bool anyFinished = false;
            bool state = false;
            foreach (var binding in _bindings)
            {
                if (!binding.UpdateDebounce(out float postDebounce))
                    continue;

                anyFinished = true;
                state |= postDebounce >= binding.PressPoint;
            }

            if (anyFinished)
                ProcessNextState(InputManager.CurrentInputTime, state);
        }

        protected override SingleButtonBinding OnControlAdded(ActuationSettings settings, InputControl<float> control)
        {
            return new(control, settings);
        }

        protected override SingleButtonBinding DeserializeControl(InputControl<float> control, SerializedInputControl serialized)
        {
            return new(control, serialized);
        }
    }
}