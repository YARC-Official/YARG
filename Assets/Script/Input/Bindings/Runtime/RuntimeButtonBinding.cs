using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using YARG.Input.Bindings;

namespace YARG.Input.Bindings
{
    public class RuntimeButtonBinding : RuntimeControlBinding<RuntimeSingleButtonBinding, float>
    {
        public bool State { get; protected set; }

        protected DebounceTimer<bool> _debounceTimer;

        public long DebounceThreshold
        {
            get => _debounceTimer.TimeThreshold;
        }

        public RuntimeButtonBinding(InputDevice controller, ReusableButtonBinding reusableBinding)
            : base(reusableBinding.Action, reusableBinding.Name)
        {
            _debounceTimer = new()
            {
                TimeThreshold = reusableBinding.DebounceThreshold
            };

            foreach (var singleBinding in reusableBinding.Bindings)
            {
                var singleRuntimeBinding = singleBinding.MakeRuntime(controller) as RuntimeSingleButtonBinding;

                if (singleRuntimeBinding is not null)
                {
                    _bindings.Add(singleRuntimeBinding);
                }
            }
        }

        public override bool IsControlActuated(InputControl<float> control)
        {
            var previousValue = control.ReadValueFromPreviousFrame();
            var currentValue = control.ReadValue();

            bool actuated = Math.Abs(currentValue - previousValue) >= AXIS_DELTA_THRESHOLD;

            if (control is ButtonControl button)
            {
                return actuated && currentValue >= button.pressPointOrDefault;
            }

            return actuated;
        }

        protected override void OnStateChanged(RuntimeSingleButtonBinding singleBinding, double time)
        {
            var state = singleBinding.IsPressed;
            foreach (var other in _bindings)
            {
                if (other == singleBinding)
                {
                    continue;
                }

                other.UpdateDebounce(time);
                state |= other.IsPressed;
            }

            // Ignore if state is unchanged
            if (state == State)
            {
                return;
            }

            // Ignore presses/releases within the debounce threshold
            _debounceTimer.UpdateValue(state);
            if (!_debounceTimer.HasElapsed(time))
            {
                return;
            }

            State = _debounceTimer.Stop();
            FireInputEvent(time, State);

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
    }
}
