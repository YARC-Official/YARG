using System.Collections.Generic;
using UnityEngine.InputSystem;
using YARG.Core;

namespace YARG.Input
{
    public class DeviceBindings
    {
        private readonly Dictionary<GameMode, GameModeBindings> _bindsByGameMode;

        public GameModeBindings MenuBinds { get; private set; }

        public InputDevice Device { get; }

        public DeviceBindings(InputDevice device)
        {
            Device = device;

            _bindsByGameMode = new();
            MenuBinds = new(device);
        }

        public GameModeBindings GetBindingsForGameMode(GameMode mode)
        {
            if (_bindsByGameMode.TryGetValue(mode, out var bindings))
            {
                return bindings;
            }

            // Create a binding object if there isn't one yet for that game mode
            var newBindings = new GameModeBindings(Device);
            _bindsByGameMode[mode] = newBindings;
            return newBindings;
        }
    }
}