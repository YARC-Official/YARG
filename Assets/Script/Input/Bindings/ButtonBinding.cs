using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using YARG.Helpers.Extensions;
using YARG.Input.Serialization;

namespace YARG.Input
{
    public class SingleButtonBinding : SingleBinding<float>
    {
        private const bool INVERT_DEFAULT = false;
        private const long DEBOUNCE_DEFAULT = 0;

        private DebounceTimer<float> _debounceTimer = new();

        private float _invertSign = 1;
        private float _pressPoint;

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

        /// <summary>
        /// The debounce time threshold, in milliseconds. Use 0 or less to disable debounce.
        /// </summary>
        public long DebounceThreshold
        {
            get => _debounceTimer.TimeThreshold;
            set => _debounceTimer.TimeThreshold = value;
        }

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
            PressPoint = control.GetPressPoint(settings);
        }

        public SingleButtonBinding(InputControl<float> control, SerializedInputControl serialized)
            : base(control, serialized)
        {
            if (!serialized.Parameters.TryGetValue(nameof(Inverted), out string invertedText) ||
                !bool.TryParse(invertedText, out bool inverted))
                inverted = INVERT_DEFAULT;

            Inverted = inverted;

            if (!serialized.Parameters.TryGetValue(nameof(PressPoint), out string pressPointText) ||
                !float.TryParse(pressPointText, out float pressPoint))
                pressPoint = control.GetPressPoint();

            PressPoint = pressPoint;

            if (!serialized.Parameters.TryGetValue(nameof(DebounceThreshold), out string debounceText) ||
                !long.TryParse(debounceText, out long debounceThreshold))
                debounceThreshold = DEBOUNCE_DEFAULT;

            DebounceThreshold = debounceThreshold;
        }

        public override SerializedInputControl Serialize()
        {
            var serialized = base.Serialize();
            if (serialized is null)
                return null;

            if (Inverted != INVERT_DEFAULT)
                serialized.Parameters.Add(nameof(Inverted), Inverted.ToString().ToLower());
            if (Math.Abs(PressPoint - Control.GetPressPoint()) >= 0.001)
                serialized.Parameters.Add(nameof(PressPoint), PressPoint.ToString());
            if (DebounceThreshold != DEBOUNCE_DEFAULT)
                serialized.Parameters.Add(nameof(DebounceThreshold), DebounceThreshold.ToString());

            return serialized;
        }

        public override void UpdateState()
        {
            PreviousState = State;

            // Read new state
            RawState = Control.value;
            _debounceTimer.Update(CalculateState(RawState));

            // Wait for debounce to end
            if (!_debounceTimer.HasElapsed)
                return;

            _debounceTimer.Restart();
            State = _debounceTimer.Value;

            InvokeStateChanged(State);
        }

        private float CalculateState(float rawValue)
        {
            return rawValue * _invertSign;
        }

        public bool UpdateDebounce()
        {
            if (!_debounceTimer.HasElapsed)
                return false;

            _debounceTimer.Reset();
            State = _debounceTimer.Value;
            InvokeStateChanged(State);
            return true;
        }
    }

    public class ButtonBinding : ControlBinding<float, SingleButtonBinding>
    {
        protected const long DEBOUNCE_DEFAULT = 0;

        protected DebounceTimer<bool> _debounceTimer = new();

        public long DebounceThreshold
        {
            get => _debounceTimer.TimeThreshold;
            set => _debounceTimer.TimeThreshold = value;
        }

        protected bool _currentValue;

        public ButtonBinding(string name, int action) : base(name, action)
        {
        }

        protected override Dictionary<string, string> SerializeParameters()
        {
            var parameters = new Dictionary<string, string>();

            if (DebounceThreshold != DEBOUNCE_DEFAULT)
                parameters.Add(nameof(DebounceThreshold), DebounceThreshold.ToString());

            return parameters;
        }

        protected override void DeserializeParameters(Dictionary<string, string> parameters)
        {
            if (!parameters.TryGetValue(nameof(DebounceThreshold), out string debounceText) ||
                !long.TryParse(debounceText, out long debounce))
                debounce = DEBOUNCE_DEFAULT;

            DebounceThreshold = debounce;
        }

        public override bool IsControlActuated(ActuationSettings settings, InputControl<float> control)
        {
            float previousValue = control.ReadValueFromPreviousFrame();
            float value = control.ReadValue();
            bool actuated = Math.Abs(value - previousValue) >= settings.AxisDeltaThreshold;

            if (control is ButtonControl button)
                return actuated && value >= button.pressPointOrDefault;
            else
                return actuated;
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

            // Ignore repeat presses/releases within the debounce threshold
            _debounceTimer.Update(state);
            if (!_debounceTimer.HasElapsed)
                return;

            _debounceTimer.Restart();
            _currentValue = _debounceTimer.Value;
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