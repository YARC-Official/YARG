using UnityEngine;
using UnityEngine.InputSystem.LowLevel;

namespace YARG.Input
{
    public class AxisBindingParameters
    {
    }

    public class AxisBinding : ControlBinding<float, AxisBindingParameters>
    {
        private float _currentValue;

        public AxisBinding(string name, int action) : base(name, action)
        {
        }

        public override void ProcessInputEvent(InputEventPtr eventPtr)
        {
            float max = 0f;
            foreach (var binding in _bindings)
            {
                var value = binding.UpdateState(eventPtr);
                if (value > max)
                    max = value;
            }

            ProcessNextState(eventPtr.time, max);
        }

        private void ProcessNextState(double time, float state)
        {
            // Ignore if state is unchanged
            if (Mathf.Approximately(_currentValue, state))
                return;

            _currentValue = state;
            FireEvent(time, state);
        }
    }
}