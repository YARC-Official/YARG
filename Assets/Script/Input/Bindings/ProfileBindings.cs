using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using YARG.Core;
using YARG.Core.Game;

namespace YARG.Input
{
    public class ProfileBindings
    {
        public YargProfile Profile { get; }

        private readonly Dictionary<GameMode, GameModeBindings> _bindsByGameMode = new();

        private readonly List<InputDevice> _devices = new();

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

        public ProfileBindings(YargProfile profile)
        {
            Profile = profile;
            foreach (var mode in EnumExtensions<GameMode>.Values)
            {
                _bindsByGameMode.Add(mode, new(mode));
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

        public void SubscribeToGameplayInputs(GameMode mode, GameInputProcessed onInputProcessed)
        {
            _bindsByGameMode[mode].Gameplay.InputProcessed += onInputProcessed;
        }

        public void UnsubscribeFromGameplayInputs(GameMode mode, GameInputProcessed onInputProcessed)
        {
            _bindsByGameMode[mode].Gameplay.InputProcessed -= onInputProcessed;
        }

        public bool AddDevice(InputDevice device)
        {
            if (_devices.Contains(device))
                return false;

            _devices.Add(device);
            return true;
        }

        public bool ContainsDevice(InputDevice device)
        {
            return _devices.Contains(device);
        }

        public bool RemoveDevice(InputDevice device)
        {
            return _devices.Remove(device);
        }

        public void ProcessInputEvent(InputEventPtr eventPtr)
        {
            var device = InputSystem.GetDeviceById(eventPtr.deviceId);
            if (!_devices.Contains(device))
                throw new InvalidOperationException($"Device {device} is not paired to profile {Profile.Name}!");

            foreach (var bindings in _bindsByGameMode.Values)
            {
                bindings.ProcessInputEvent(eventPtr);
            }
        }
    }
}