using System;
using System.Collections.Generic;
using YARG.Core.Input;
using YARG.Core.Logging;

namespace YARG.Input.Bindings
{
    public class RuntimeInputAggregator
    {
        private readonly List<RuntimeBindingSet> _sources = new();

        private readonly Dictionary<int, bool> _buttonStates = new();
        private readonly Dictionary<int, int> _activeButtonCounts = new();

        private readonly Dictionary<int, float> _axisStates = new();
        private readonly Dictionary<int, float> _newAxisStates = new();

        private readonly Dictionary<int, int> _integerStates = new();
        private readonly Dictionary<int, int> _newIntegerStates = new();

        public event GameInputProcessed InputProcessed;

        public void Add(RuntimeBindingSet source)
        {
            if (_sources.Contains(source))
            {
                return;
            }

            _sources.Add(source);

            foreach (var binding in source)
            {
                if (binding is RuntimeImpulseBinding impulse)
                {
                    impulse.Pressed += OnButtonPressed;
                    impulse.InputProcessed += OnButtonInputProcessed;

                    if (impulse.State)
                    {
                        _activeButtonCounts[impulse.Action] =
                            _activeButtonCounts.GetValueOrDefault(impulse.Action) + 1;

                        _buttonStates[impulse.Action] = true;
                    }
                }
                else if (binding is RuntimeButtonBinding button)
                {
                    button.InputProcessed += OnButtonInputProcessed;

                    if (button.State)
                    {
                        _activeButtonCounts[button.Action] =
                            _activeButtonCounts.GetValueOrDefault(button.Action) + 1;

                        _buttonStates[button.Action] = true;
                    }
                }
            }
        }

        public void Remove(RuntimeBindingSet source)
        {
            _sources.Remove(source);

            foreach (var binding in source)
            {
                if (binding is RuntimeImpulseBinding impulse)
                {
                    impulse.Pressed -= OnButtonPressed;
                    impulse.InputProcessed -= OnButtonInputProcessed;

                    if (impulse.State)
                    {
                        var count = _activeButtonCounts.GetValueOrDefault(impulse.Action);
                        count--;

                        if (count <= 0)
                        {
                            _activeButtonCounts.Remove(impulse.Action);
                            _buttonStates[impulse.Action] = false;
                        }
                        else
                        {
                            _activeButtonCounts[impulse.Action] = count;
                        }
                    }
                }
                else if (binding is RuntimeButtonBinding button)
                {
                    button.InputProcessed -= OnButtonInputProcessed;

                    if (button.State)
                    {
                        var count = _activeButtonCounts.GetValueOrDefault(button.Action);
                        count--;

                        if (count <= 0)
                        {
                            _activeButtonCounts.Remove(button.Action);
                            _buttonStates[button.Action] = false;
                        }
                        else
                        {
                            _activeButtonCounts[button.Action] = count;
                        }
                    }
                }
            }
        }

        public void UpdateForFrame(double time)
        {
            _newAxisStates.Clear();
            _newIntegerStates.Clear();

            foreach (var source in _sources)
            {
                foreach (var binding in source)
                {
                    switch (binding) {
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


            // Automatically zero-out axis actions that were nonzero last frame but no longer have
            // any active source reporting them
            var releasedAxes = new List<int>();
            foreach (var (action, oldState) in _axisStates)
            {
                if (oldState is not 0 && !_newAxisStates.ContainsKey(action))
                {
                    releasedAxes.Add(action);
                }
            }
            foreach (var action in releasedAxes)
            {
                _axisStates[action] = 0;

                var input = new GameInput(time, action, 0f);
                InputProcessed?.Invoke(ref input);
            }

            // Automatically zero-out integer actions that were nonzero last frame but no longer have
            // any active source reporting them
            var releasedIntegers = new List<int>();
            foreach (var (action, oldState) in _integerStates)
            {
                if (oldState is not 0 && !_newIntegerStates.ContainsKey(action))
                {
                    releasedIntegers.Add(action);
                }
            }
            foreach (var action in releasedIntegers)
            {
                _integerStates[action] = 0;

                var input = new GameInput(time, action, 0);
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

        private void OnButtonPressed(ref GameInput input)
        {
            if (_buttonStates.TryGetValue(input.Action, out var state) && state)
            {
                InputProcessed?.Invoke(ref input);
            }
        }

        private void OnButtonInputProcessed(ref GameInput input)
        {
            var count = _activeButtonCounts.GetValueOrDefault(input.Action);

            if (input.Button)
            {
                count++;
            }
            else
            {
                count--;
            }

            _activeButtonCounts[input.Action] = count;

            var newState = count > 0;

            if (_buttonStates.GetValueOrDefault(input.Action) == newState)
            {
                return;
            }

            _buttonStates[input.Action] = newState;

            InputProcessed?.Invoke(ref input);
        }
    }
}
