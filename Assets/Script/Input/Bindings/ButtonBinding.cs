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

        private float _pressPoint;
        public float PressPoint
        {
            get => _pressPoint;
            set
            {
                bool pressed = IsPressed;
                _pressPoint = value;

                // This state change won't be propogated to the main binding, however calibration settings
                // should never be changed outside of the binding menu, so that should be fine
                if (pressed != IsPressed)
                {
                    State = CalculateState(RawState);
                    InvokeStateChanged(State);
                }
            }
        }

        private float _invertSign = 1;
        public bool Inverted
        {
            get => float.IsNegative(_invertSign);
            set
            {
                bool inverted = Inverted;
                _invertSign = value ? -1 : 1;

                // (see above)
                if (inverted != Inverted)
                {
                    State = CalculateState(RawState);
                    InvokeStateChanged(State);
                }
            }
        }

        private float _postDebounceValue;

        public float RawState { get; private set; }
        public float PreviousState { get; private set; }

        public bool IsPressed => State >= PressPoint;
        public bool IsPressedRaw => RawState >= PressPoint;

        public bool WasPreviouslyPressed => PreviousState >= PressPoint;

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
            if (!serialized.Parameters.TryGetValue(nameof(Inverted), out string invertedText) ||
                !bool.TryParse(invertedText, out bool inverted))
                inverted = false;

            Inverted = inverted;

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

            serialized.Parameters.Add(nameof(Inverted), Inverted.ToString().ToLower());
            serialized.Parameters.Add(nameof(PressPoint), PressPoint.ToString());
            serialized.Parameters.Add(nameof(DebounceThreshold), DebounceThreshold.ToString());

            return serialized;
        }

        public override void UpdateState()
        {
            PreviousState = State;

            // Read new state
            RawState = Control.value;
            _postDebounceValue = CalculateState(RawState);

            // Check debounce
            if (_debounceTimer.IsRunning && _debounceTimer.ElapsedMilliseconds < DebounceThreshold)
                // Wait for when debounce ends
                return;

            // Stop debounce and process this event normally
            _debounceTimer.Reset();
            State = _postDebounceValue;

            // Start debounce again if enabled
            if (DebounceEnabled)
                _debounceTimer.Start();

            InvokeStateChanged(State);
        }

        private float CalculateState(float rawValue)
        {
            return rawValue * _invertSign;
        }

        public bool UpdateDebounce()
        {
            // Ignore if debounce is disabled
            if (!DebounceEnabled)
                return false;

            // Check time elapsed
            if (_debounceTimer.ElapsedMilliseconds >= DebounceThreshold)
            {
                // Stop timer and process post-debounce value
                _debounceTimer.Reset();
                State = _postDebounceValue;
                InvokeStateChanged(State);
                return true;
            }

            return false;
        }
    }

    public class ButtonBinding : ControlBinding<float, SingleButtonBinding>
    {
        protected bool _currentValue;

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

        protected override void OnStateChanged(SingleButtonBinding _, double time)
        {
            bool state = false;
            foreach (var binding in _bindings)
            {
                state |= binding.IsPressed;
            }

            ProcessNextState(time, state);
        }

        private void ProcessNextState(double time, bool state)
        {
            // Ignore if state is unchanged
            if (_currentValue == state)
                return;

            _currentValue = state;
            FireInputEvent(time, state);
        }

        public override void UpdateForFrame(double updateTime)
        {
            UpdateDebounce(updateTime);
        }

        private void UpdateDebounce(double updateTime)
        {
            bool anyFinished = false;
            bool state = false;
            foreach (var binding in _bindings)
            {
                if (!binding.UpdateDebounce())
                    continue;

                anyFinished = true;
                state |= binding.IsPressed;
            }

            if (anyFinished)
                ProcessNextState(updateTime, state);
        }

        protected override SingleButtonBinding CreateBinding(ActuationSettings settings, InputControl<float> control)
        {
            return new(control, settings);
        }

        protected override SingleButtonBinding DeserializeControl(InputControl<float> control, SerializedInputControl serialized)
        {
            return new(control, serialized);
        }
    }
}