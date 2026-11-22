using System;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using YARG.Core.Input;
using YARG.Core.Logging;

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
                OnBindingDebounceUpdated(other, time);
                state |= other.IsPressed;
            }

            // Ignore presses/releases within the debounce threshold
            _debounceTimer.UpdateValue(state);

            // Ignore if state is unchanged
            if (state == State)
            {
                return;
            }

            if (!_debounceTimer.HasElapsed(time))
            {
                return;
            }

            State = _debounceTimer.Stop();
            FireStateInputEvent(time, State);

            // Only start collective debounce on button press
            if (State && !_debounceTimer.IsRunning)
                _debounceTimer.Start(time);
        }

        public override void UpdateForFrame(double updateTime)
        {
            UpdateDebounce(updateTime);
        }

        protected virtual void FireStateInputEvent(double time, bool state)
        {
            FireInputEvent(time, state);
        }

        /// <summary>
        /// Called after <paramref name="singleBinding"/> had its debounce window resolved, so
        /// subclasses can react to states that were deferred past the originating input event.
        /// </summary>
        protected virtual void OnBindingDebounceUpdated(RuntimeSingleButtonBinding singleBinding, double time)
        {
        }

        private void UpdateDebounce(double updateTime)
        {
            // Update individual debounces
            bool collectiveState = false;
            foreach (var binding in _bindings)
            {
                binding.UpdateDebounce(updateTime);
                OnBindingDebounceUpdated(binding, updateTime);
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
            FireStateInputEvent(updateTime, state);
            FireStateChanged();
        }
    }

    // An impulse binding is like a button binding, with two changes:
    //  * It sends a Pressed event whenever any of its SingleBindings is pressed, even if
    //       another is already held. State is still aggregated, and Released events are
    //       still only fired when the last SingleBinding is released, not per individual
    //       release
    //  * It includes velocity information
    public class RuntimeImpulseBinding : RuntimeButtonBinding
    {
        public event GameInputProcessed Pressed;

        public RuntimeImpulseBinding(InputDevice controller, ReusableButtonBinding reusableBinding)
            : base(controller, reusableBinding) { }

        protected override void OnStateChanged(RuntimeSingleButtonBinding singleBinding, double time)
        {
            base.OnStateChanged(singleBinding, time);

            if (singleBinding.JustPressed)
            {
                FirePressedEvent(singleBinding, time);
            }
        }

        protected override void OnBindingDebounceUpdated(RuntimeSingleButtonBinding singleBinding, double time)
        {
            base.OnBindingDebounceUpdated(singleBinding, time);

            if (singleBinding.JustPressed)
            {
                FirePressedEvent(singleBinding, time);
            }
        }

        protected override void FireStateInputEvent(double time, bool state)
        {
            if (!state)
            {
                base.FireStateInputEvent(time, state);
            }
        }

        protected virtual void FirePressedEvent(RuntimeSingleButtonBinding singleBinding, double time)
        {
            // Use the triggering binding's state rather than the live control values, so a held
            // sibling binding cannot donate its velocity to this press.
            var velocity = Math.Abs(singleBinding.State);

            var input = new GameInput(time, Action, velocity);

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
