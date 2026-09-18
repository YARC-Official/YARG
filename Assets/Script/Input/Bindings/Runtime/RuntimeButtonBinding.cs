using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using YARG.Core.Input;
using YARG.Core.Logging;
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

    // An impulse binding is like a button binding, but it reports only presses, and does so even
    // if another one of its SingleBindings is being held.
    //
    // This honestly should probably be a sibling of RuntimeButtonBinding rather than a subclass of it,
    // but this works for now.
    public class RuntimeImpulseBinding : RuntimeButtonBinding
    {
        public event GameInputProcessed Pressed;

        public RuntimeImpulseBinding(InputDevice controller, ReusableButtonBinding reusableBinding)
            : base(controller, reusableBinding) { }

        public override void UpdateForFrame(double updateTime)
        {
            // Do nothing; we only want to send Pressed events, not regular state transitions
        }

        protected override void OnStateChanged(RuntimeSingleButtonBinding singleBinding, double time)
        {
            if (!singleBinding.IsPressed || singleBinding.WasPreviouslyPressed)
            {
                return;
            }

            if (_debounceTimer.IsRunning && !_debounceTimer.HasElapsed(time))
            {
                return;
            }

            FirePressedEvent(time);
            _debounceTimer.Start(time);
        }

        protected void FirePressedEvent(double time, float value = 1f)
        {
            var input = new GameInput(time, Action, value);

            if (!Enabled)
            {
                return;
            }

            try
            {
                Pressed?.Invoke(ref input);
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, $"Exception when firing input event for {Key}");
            }
        }
    }
}
