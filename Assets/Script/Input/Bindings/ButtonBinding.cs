using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using YARG.Helpers.Extensions;
using YARG.Input.Serialization;

namespace YARG.Input
{
    public enum DebounceMode
    {
        /// <summary>Activates on press only.</summary>
        Press,

        /// <summary>Activates on release only.</summary>
        Release,

        /// <summary>Activates on both press and release.</summary>
        PressAndRelease,
    }

    public class SingleButtonBinding : SingleBinding<float>
    {
        private const bool INVERT_DEFAULT = false;
        private const DebounceMode DEBOUNCE_MODE_DEFAULT = DebounceMode.Press;
        private const long DEBOUNCE_THRESHOLD_DEFAULT = 5;

        private DebounceTimer<float> _debounceTimer = new()
        {
            TimeThreshold = DEBOUNCE_THRESHOLD_DEFAULT,
        };

        private float _invertSign = INVERT_DEFAULT ? -1 : 1;
        private float _pressPoint;

        public bool Inverted
        {
            get => float.IsNegative(_invertSign);
            set
            {
                bool inverted = Inverted;
                _invertSign = value ? -1 : 1;

                // This state change won't be propogated to the main binding, however calibration settings
                // should never be changed outside of the binding menu, so that should be fine
                if (inverted != Inverted)
                {
                    State = CalculateState(Control.value);
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

                // (see above)
                if (pressed != IsPressed)
                {
                    State = CalculateState(Control.value);
                    InvokeStateChanged(State);
                }
            }
        }

        public DebounceMode DebounceMode { get; set; } = DEBOUNCE_MODE_DEFAULT;

        /// <summary>
        /// The debounce time threshold, in milliseconds. Use 0 or less to disable debounce.
        /// </summary>
        public long DebounceThreshold
        {
            get => _debounceTimer.TimeThreshold;
            set => _debounceTimer.TimeThreshold = value;
        }

        public float PreviousState { get; private set; }

        public bool IsPressed => State >= PressPoint;
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

            if (!serialized.Parameters.TryGetValue(nameof(DebounceMode), out string modeText) ||
                !Enum.TryParse<DebounceMode>(modeText, out var debounceMode))
                debounceMode = DEBOUNCE_MODE_DEFAULT;

            DebounceMode = debounceMode;

            if (!serialized.Parameters.TryGetValue(nameof(DebounceThreshold), out string thresholdText) ||
                !long.TryParse(thresholdText, out long debounceThreshold))
                debounceThreshold = DEBOUNCE_THRESHOLD_DEFAULT;

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
            if (DebounceMode != DEBOUNCE_MODE_DEFAULT)
                serialized.Parameters.Add(nameof(DebounceMode), DebounceMode.ToString());
            if (DebounceThreshold != DEBOUNCE_THRESHOLD_DEFAULT)
                serialized.Parameters.Add(nameof(DebounceThreshold), DebounceThreshold.ToString());

            return serialized;
        }

        public override void UpdateState(double time)
        {
            PreviousState = State;

            // Read new state
            _debounceTimer.UpdateValue(CalculateState(Control.value));

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

        public override void ResetState()
        {
            PreviousState = default;
            State = default;
            _debounceTimer.Stop();
            InvokeStateChanged(State);
        }

        private float CalculateState(float rawValue)
        {
            return rawValue * _invertSign;
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

    public class ButtonBinding : ControlBinding<float, SingleButtonBinding>
    {
        protected const long DEBOUNCE_DEFAULT = 5;

        protected DebounceTimer<bool> _debounceTimer = new()
        {
            TimeThreshold = DEBOUNCE_DEFAULT,
        };

        public long DebounceThreshold
        {
            get => _debounceTimer.TimeThreshold;
            set => _debounceTimer.TimeThreshold = value;
        }

        public bool State { get; protected set; }

        public ButtonBinding(string name, int action) : base(name, action)
        {
        }

        public ButtonBinding(string name, string nameLefty, int action) : base(name, nameLefty, action)
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

        protected override void OnStateChanged(SingleButtonBinding binding, double time)
        {
            bool state = binding.IsPressed;
            foreach (var other in _bindings)
            {
                if (other == binding)
                    continue;

                other.UpdateDebounce(time);
                state |= other.IsPressed;
            }

            // Ignore if state is unchanged
            if (state == State)
                return;

            // Ignore presses/releases within the debounce threshold
            _debounceTimer.UpdateValue(state);
            if (!_debounceTimer.HasElapsed(time))
                return;

            State = _debounceTimer.Stop();
            FireInputEvent(time, State);

            // Already fired in ControlBinding
            // FireStateChanged();

            // Only start collective debounce on button press
            if (State && !_debounceTimer.IsRunning)
                _debounceTimer.Start(time);
        }

        public override void UpdateForFrame(double updateTime)
        {
            UpdateDebounce(updateTime);
        }

        private void UpdateDebounce(double updateTime)
        {
            // Update individual debounces
            bool collectiveState = false;
            foreach (var binding in _bindings)
            {
                binding.UpdateDebounce(updateTime);
                collectiveState |= binding.IsPressed;
            }

            // Ignore presses/releases within the debounce threshold
            _debounceTimer.UpdateValue(collectiveState);
            if (!_debounceTimer.HasElapsed(updateTime))
                return;

            bool state = _debounceTimer.Stop();
            // Ignore if state is unchanged
            if (state == State)
                return;

            State = state;
            FireInputEvent(updateTime, state);
            FireStateChanged();
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