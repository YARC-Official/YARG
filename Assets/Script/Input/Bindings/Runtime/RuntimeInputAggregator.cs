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

            foreach (var source in _sources)
            {
                foreach (var binding in source)
                {
                    if (binding is not RuntimeButtonBinding button)
                    {
                        continue;
                    }

                    if (_newButtonStates.TryGetValue(button.Action, out var state))
                    {
                        _newButtonStates[button.Action] = state || button.State;
                    }
                    else
                    {
                        _newButtonStates[button.Action] = button.State;
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
        }
    }
}
