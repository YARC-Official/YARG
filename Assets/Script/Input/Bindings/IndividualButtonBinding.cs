using System.Collections.Generic;

namespace YARG.Input
{
    /// <summary>
    /// A button binding which sends press events for each control individually.
    /// </summary>
    public class IndividualButtonBinding : ButtonBinding
    {
        public IndividualButtonBinding(string name, int action) : base(name, action)
        {
        }

        public IndividualButtonBinding(string name, string nameLefty, int action) : base(name, nameLefty, action)
        {
        }

        protected override void OnStateChanged(SingleButtonBinding binding, double time)
        {
            // Update debounce on all bindings
            bool othersPressed = false;
            foreach (var other in _bindings)
            {
                if (other == binding)
                    continue;

                other.UpdateDebounce(time);
                othersPressed |= other.IsPressed;
            }

            bool pressed = binding.IsPressed;
            // For axes/analog buttons
            if (pressed == binding.WasPreviouslyPressed)
                return;

            // Don't send a release event until all other bindings are released
            if (!pressed && othersPressed)
                return;

            // Ignore presses/releases within the debounce threshold
            _debounceTimer.UpdateValue(pressed);
            if (!_debounceTimer.HasElapsed(time))
                return;

            State = _debounceTimer.Stop();
            FireInputEvent(time, State);

            // Already fired in ControlBinding
            // FireStateChanged();

            // Only start debounce on button press
            if (State && !_debounceTimer.IsRunning)
                _debounceTimer.Start(time);
        }
    }
}