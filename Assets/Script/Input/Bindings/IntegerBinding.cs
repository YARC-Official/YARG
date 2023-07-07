using System;
using UnityEngine.InputSystem.LowLevel;

namespace YARG.Input
{
    public enum IntegerBindingMode
    {
        Average,
        Max,
    }

    public class IntegerBindingParameters
    {
    }

    public class IntegerBinding : ControlBinding<int, IntegerBindingParameters>
    {
        public IntegerBindingMode Mode { get; }

        private int _currentValue;

        public IntegerBinding(string displayName, int action) : base(displayName, action)
        {
        }

        public override void ProcessInputEvent(InputEventPtr eventPtr)
        {
            int value = Mode switch
            {
                IntegerBindingMode.Average => ProcessUsingAverage(eventPtr),
                IntegerBindingMode.Max => ProcessUsingMax(eventPtr),
                _ => throw new NotImplementedException($"Unhandled integer mode {Mode}!")
            };

            ProcessNextState(eventPtr.time, value);
        }

        private int ProcessUsingAverage(InputEventPtr eventPtr)
        {
            int cumulative = 0;
            foreach (var binding in _bindings)
            {
                var value = binding.UpdateState(eventPtr);
                cumulative += value;
            }

            return cumulative / _bindings.Count;
        }

        private int ProcessUsingMax(InputEventPtr eventPtr)
        {
            int max = 0;
            foreach (var binding in _bindings)
            {
                var value = binding.UpdateState(eventPtr);
                if (value > max)
                    max = value;
            }

            return max;
        }

        private void ProcessNextState(double time, int state)
        {
            // Ignore if state is unchanged
            if (_currentValue == state)
                return;

            _currentValue = state;
            FireEvent(time, state);
        }
    }
}