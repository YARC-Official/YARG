using System;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;

namespace YARG.Input
{
    public enum AxisBindingMode
    {
        Average,
        Max,
    }

    public class AxisBindingParameters
    {
    }

    public class AxisBinding : ControlBinding<float, AxisBindingParameters>
    {
        public AxisBindingMode Mode { get; set; }

        private float _currentValue;

        public AxisBinding(string displayName, int action) : base(displayName, action)
        {
        }

        public override void ProcessInputEvent(InputEventPtr eventPtr)
        {
            float value = Mode switch
            {
                AxisBindingMode.Average => ProcessUsingAverage(eventPtr),
                AxisBindingMode.Max => ProcessUsingMax(eventPtr),
                _ => throw new NotImplementedException($"Unhandled axis mode {Mode}!")
            };

            ProcessNextState(eventPtr.time, value);
        }

        private float ProcessUsingAverage(InputEventPtr eventPtr)
        {
            float cumulative = 0f;
            foreach (var binding in _bindings)
            {
                var value = binding.UpdateState(eventPtr);
                cumulative += value;
            }

            return cumulative / _bindings.Count;
        }

        private float ProcessUsingMax(InputEventPtr eventPtr)
        {
            float max = 0f;
            foreach (var binding in _bindings)
            {
                var value = binding.UpdateState(eventPtr);
                if (value > max)
                    max = value;
            }

            return max;
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