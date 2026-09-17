using System;
using System.Collections.Generic;
using System.Text;
using YARG.Core.Input;

namespace YARG.Input.Bindings
{
    public class RuntimeInputAggregator
    {
        private readonly List<RuntimeBindingSet> _sources = new();

        private readonly Dictionary<int, bool> _buttonStates = new();
        private readonly Dictionary<int, bool> _newButtonStates = new();

        private readonly Dictionary<int, float> _axisStates = new();
        private readonly Dictionary<int, float> _newAxisStates = new();

        private readonly Dictionary<int, int> _integerStates = new();
        private readonly Dictionary<int, int> _newIntegerStates = new();

        public event GameInputProcessed InputProcessed;

        public void Add(RuntimeBindingSet source)
        {
            if (!_sources.Contains(source))
            {
                _sources.Add(source);
            }
        }

        public void Remove(RuntimeBindingSet source)
        {
            _sources.Remove(source);
        }

        public void UpdateForFrame(double time)
        {
            _newButtonStates.Clear();
            _newAxisStates.Clear();

            foreach (var source in _sources)
            {
                foreach (var binding in source)
                {
                    switch (binding) {
                        case RuntimeButtonBinding button:
                            if (_newButtonStates.TryGetValue(button.Action, out var buttonState))
                            {
                                _newButtonStates[button.Action] = buttonState || button.State;
                            }
                            else
                            {
                                _newButtonStates[button.Action] = button.State;
                            }
                            break;
                        case RuntimeAxisBinding axis:
                            if (!_newAxisStates.TryGetValue(axis.Action, out var axisState) ||
                                    Math.Abs(axis.State) > Math.Abs(axisState))
                            {
                                _newAxisStates[axis.Action] = axis.State;
                            }
                            break;
                        case RuntimeIntegerBinding integer:
                            if (!_newIntegerStates.TryGetValue(integer.Action, out var intState) ||
                                    integer.State > intState)
                            {
                                _newIntegerStates[integer.Action] = integer.State;
                            }
                            break;
                    }
                }
            }

            foreach (var (action, newState) in _newButtonStates)
            {
                _buttonStates.TryGetValue(action, out var oldState);

                if (oldState == newState)
                {
                    continue;
                }

                _buttonStates[action] = newState;

                var input = new GameInput(time, action, newState);
                InputProcessed?.Invoke(ref input);
            }

            foreach (var (action, newState) in _newAxisStates)
            {
                _axisStates.TryGetValue(action, out var oldState);

                if (oldState == newState)
                {
                    continue;
                }

                _axisStates[action] = newState;

                var input = new GameInput(time, action, newState);
                InputProcessed?.Invoke(ref input);
            }

            foreach (var (action, newState) in _newIntegerStates)
            {
                _integerStates.TryGetValue(action, out var oldState);

                if (oldState == newState)
                {
                    continue;
                }

                _integerStates[action] = newState;

                var input = new GameInput(time, action, newState);
                InputProcessed?.Invoke(ref input);
            }
        }
    }
}
