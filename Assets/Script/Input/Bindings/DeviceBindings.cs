using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using YARG.Core;

namespace YARG.Input
{
    public class DeviceBindings
    {
        private readonly Dictionary<GameMode, GameModeBindings> _bindsByGameMode = new();

        public GameModeBindings MenuBinds { get; private set; }

        public InputDevice Device { get; }

        public DeviceBindings(InputDevice device)
        {
            Device = device;

            MenuBinds = new(device);
        }

        public void SubscribeToInputsForGameMode(GameMode mode, GameInputProcessed onInputProcessed)
        {
            var modeBindings = TryGetBindingsForGameMode(mode);
            if (modeBindings is null)
                return;

            modeBindings.InputProcessed += onInputProcessed;
        }

        public void UnsubscribeToInputsForGameMode(GameMode mode, GameInputProcessed onInputProcessed)
        {
            var modeBindings = TryGetBindingsForGameMode(mode);
            if (modeBindings is null)
                return;

            modeBindings.InputProcessed -= onInputProcessed;
        }

        public bool AddBindingsForGameMode(GameMode mode, GameModeBindings bindings)
        {
            if (ContainsBindingsForGameMode(mode))
                return false;

            _bindsByGameMode.Add(mode, bindings);
            return true;
        }

        public void AddOrReplaceBindingsForGameMode(GameMode mode, GameModeBindings bindings)
        {
            RemoveBindingsForGameMode(mode);
            AddBindingsForGameMode(mode, bindings);
        }

        public bool ContainsBindingsForGameMode(GameMode mode)
        {
            return _bindsByGameMode.ContainsKey(mode);
        }

        public GameModeBindings TryGetBindingsForGameMode(GameMode mode)
        {
            return _bindsByGameMode.TryGetValue(mode, out var bindings) ? bindings : null;
        }

        public bool RemoveBindingsForGameMode(GameMode mode)
        {
            return _bindsByGameMode.Remove(mode);
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