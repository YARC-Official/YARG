using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using YARG.Core;

namespace YARG.Input
{
    public class DeviceBindings
    {
        private readonly Dictionary<GameMode, GameModeBindings> _bindsByGameMode = new();

        public GameModeBindings this[GameMode mode] => _bindsByGameMode[mode];

        public event GameInputProcessed MenuInputProcessed
        {
            add
            {
                foreach (var bindings in _bindsByGameMode.Values)
                {
                    bindings.Menu.InputProcessed += value;
                }
            }
            remove
            {
                foreach (var bindings in _bindsByGameMode.Values)
                {
                    bindings.Menu.InputProcessed -= value;
                }
            }
        }

        public InputDevice Device { get; }

        public DeviceBindings(InputDevice device)
        {
            Device = device;
            foreach (var mode in EnumExtensions<GameMode>.Values)
            {
                var menuBindings = BindingCollection.CreateMenuBindings();
                var gameplayBindings = BindingCollection.CreateGameplayBindings(mode);
                _bindsByGameMode.Add(mode, new GameModeBindings(menuBindings, gameplayBindings));
            }
        }

        public void EnableInputs()
        {
            foreach (var bindings in _bindsByGameMode.Values)
            {
                bindings.EnableInputs();
            }
        }

        public void DisableInputs()
        {
            foreach (var bindings in _bindsByGameMode.Values)
            {
                bindings.DisableInputs();
            }
        }

        public void ProcessInputEvent(InputEventPtr eventPtr)
        {
            foreach (var bindings in _bindsByGameMode.Values)
            {
                bindings.ProcessInputEvent(eventPtr);
            }
        }
    }
}