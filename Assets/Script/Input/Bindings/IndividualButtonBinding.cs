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

        protected override void OnStateChanged(SingleButtonBinding binding, double time)
        {
            bool pressed = binding.IsPressed;
            // For axes/analog buttons
            if (pressed == binding.WasPreviouslyPressed)
                return;

            // Don't send a release event until all other bindings are released
            if (!pressed)
            {
                foreach (var otherBinding in _bindings)
                {
                    if (otherBinding.IsPressed)
                        return;
                }
            }

            // Ignore repeat presses/releases within the debounce threshold
            _debounceTimer.Update(pressed);
            if (!_debounceTimer.HasElapsed)
                return;

            State = _debounceTimer.Restart();
            FireInputEvent(time, pressed);

            // Already fired in ControlBinding
            // FireStateChanged();
        }
    }
}