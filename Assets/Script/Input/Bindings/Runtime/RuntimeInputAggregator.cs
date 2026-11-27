using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using YARG.Core.Input;
using YARG.Core.Logging;

namespace YARG.Input.Bindings
{
    public class RuntimeInputAggregator
    {
        private readonly List<RuntimeBindingSet> _sources = new();

        private readonly Dictionary<int, HashSet<RuntimeButtonBinding>> _pressedButtons = new();
        private readonly Dictionary<RuntimeButtonBinding, GameInputProcessed> _buttonHandlers = new();

        private readonly Dictionary<int, float> _axisStates = new();
        private readonly Dictionary<int, float> _newAxisStates = new();

        private readonly Dictionary<int, int> _integerStates = new();
        private readonly Dictionary<int, int> _newIntegerStates = new();

        public event GameInputProcessed InputProcessed;

        private InputDevice _suppressedController;
        public void SuppressController(InputDevice controller) {
            _suppressedController = controller;
        }

        public void UnsuppressController()
        {
            _suppressedController = null;
        }

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
                }
                else if (binding is RuntimeButtonBinding button)
                {
                    GameInputProcessed handler = (ref GameInput input) => OnButtonInputProcessed(source, button, ref input);
                    _buttonHandlers[button] = handler;
                    button.InputProcessed += handler;

                    if (button.State)
                    {
                        GetPressedButtons(button.Action).Add(button);
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
                }
                else if (binding is RuntimeButtonBinding button)
                {
                    button.InputProcessed -= _buttonHandlers[button];
                    _buttonHandlers.Remove(button);
                    if (_pressedButtons.TryGetValue(button.Action, out var pressed))
                    {
                        pressed.Remove(button);
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
                if (source.Controller == _suppressedController)
                {
                    continue;
                }

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
            InputProcessed?.Invoke(ref input);
        }

        private void OnButtonInputProcessed(RuntimeBindingSet source, RuntimeButtonBinding button, ref GameInput input)
        {
            if (source.Controller == _suppressedController)
            {
                return;
            }

            var pressed = GetPressedButtons(input.Action);
            bool wasPressed = pressed.Count > 0;

            if (input.Button)
            {
                pressed.Add(button);
            }
            else
            {
                pressed.Remove(button);
            }

            // Only the first press and the last release of an action are passed on
            if (pressed.Count > 0 == wasPressed)
            {
                return;
            }

            InputProcessed?.Invoke(ref input);
        }

        private HashSet<RuntimeButtonBinding> GetPressedButtons(int action)
        {
            if (!_pressedButtons.TryGetValue(action, out var pressed))
            {
                pressed = new();
                _pressedButtons[action] = pressed;
            }

            return pressed;
        }
    }
}
