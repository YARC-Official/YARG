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

            ProcessNextState(time, pressed);
        }

        private void ProcessNextState(double time, bool state)
        {
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

            // Only send a post-debounce event if the state changed
            if (anyFinished && state != _currentValue)
                ProcessNextState(updateTime, state);
        }
    }
}