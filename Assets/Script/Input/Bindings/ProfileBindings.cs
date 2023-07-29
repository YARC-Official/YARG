using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using YARG.Core;
using YARG.Core.Game;
using YARG.Input.Serialization;

namespace YARG.Input
{
    [JsonConverter(typeof(ProfileBindingsConverter))]
    public class ProfileBindings
    {
        public YargProfile Profile { get; }

        private readonly Dictionary<GameMode, GameModeBindings> _bindsByGameMode = new();

        private readonly Dictionary<string, InputDevice> _devices = new();

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

        public ProfileBindings(YargProfile profile, SerializedProfileBindings bindings)
            : this(profile)
        {
            foreach (string serial in bindings.DeviceSerials)
            {
                // Devices will be resolved later
                _devices.Add(serial, null);
            }

            foreach (var (mode, serializedMode) in bindings.Bindings)
            {
                if (!_bindsByGameMode.TryGetValue(mode, out var modeBindings))
                {
                    Debug.LogWarning($"Encountered invalid game mode {mode}!");
                    continue;
                }

                modeBindings.Deserialize(serializedMode);
            }
        }

        public SerializedProfileBindings Serialize()
        {
            var serialized = new SerializedProfileBindings();

            foreach (var serial in _devices.Keys)
            {
                serialized.DeviceSerials.Add(serial);
            }

            foreach (var (mode, bindings) in _bindsByGameMode)
            {
                serialized.Bindings.Add(mode, bindings.Serialize());
            }

            return serialized;
        }

        public static ProfileBindings Deserialize(YargProfile profile, SerializedProfileBindings serialized)
        {
            return new(profile, serialized);
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
            string serial = device.GetSerial();
            if (_devices.ContainsKey(serial))
                return false;

            _devices.Add(serial, device);
            return true;
        }

        public bool ContainsDevice(InputDevice device)
        {
            return _devices.ContainsKey(device.GetSerial());
        }

        public bool RemoveDevice(InputDevice device)
        {
            return _devices.Remove(device.GetSerial());
        }

        public void ProcessInputEvent(InputEventPtr eventPtr)
        {
            var device = InputSystem.GetDeviceById(eventPtr.deviceId);
            if (!_devices.ContainsKey(device.GetSerial()))
                throw new InvalidOperationException($"Device {device} is not paired to profile {Profile.Name}!");

            foreach (var bindings in _bindsByGameMode.Values)
            {
                bindings.ProcessInputEvent(eventPtr);
            }
        }
    }
}