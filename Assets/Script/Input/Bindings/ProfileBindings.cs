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

        public event Action BindingsChanged
        {
            add
            {
                foreach (var bindings in _bindsByGameMode.Values)
                    bindings.BindingsChanged += value;
            }
            remove
            {
                foreach (var bindings in _bindsByGameMode.Values)
                    bindings.BindingsChanged -= value;
            }
        }

        public event GameInputProcessed MenuInputProcessed
        {
            add
            {
                foreach (var bindings in _bindsByGameMode.Values)
                    bindings.Menu.InputProcessed += value;
            }
            remove
            {
                foreach (var bindings in _bindsByGameMode.Values)
                    bindings.Menu.InputProcessed -= value;
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
            if (ContainsDevice(device))
                return false;

            _devices[serial] = device;
            NotifyDeviceAdded(device);
            return true;
        }

        public bool ContainsDevice(InputDevice device)
        {
            string serial = device.GetSerial();
            return _devices.TryGetValue(serial, out var registered) && registered is not null;
        }

        public bool RemoveDevice(InputDevice device)
        {
            string serial = device.GetSerial();
            if (!_devices.Remove(serial))
                return false;

            NotifyDeviceRemoved(device);
            return true;
        }

        public void OnDeviceAdded(InputDevice device)
        {
            // Ignore already-added devices
            if (ContainsDevice(device))
                return;

            string serial = device.GetSerial();
            _devices[serial] = device;
            NotifyDeviceAdded(device);
        }

        public void OnDeviceRemoved(InputDevice device)
        {
            // Ignore devices not registered to this profile
            if (!ContainsDevice(device))
                return;

            // Need to retain the serial for serialization
            string serial = device.GetSerial();
            _devices[serial] = null;
            NotifyDeviceRemoved(device);
        }

        private void NotifyDeviceAdded(InputDevice device)
        {
            foreach (var bindings in _bindsByGameMode.Values)
            {
                bindings.OnDeviceAdded(device);
            }
        }

        private void NotifyDeviceRemoved(InputDevice device)
        {
            foreach (var bindings in _bindsByGameMode.Values)
            {
                bindings.OnDeviceRemoved(device);
            }
        }

        public void ProcessInputEvent(InputEventPtr eventPtr)
        {
            var device = InputSystem.GetDeviceById(eventPtr.deviceId);
            if (!ContainsDevice(device))
                throw new InvalidOperationException($"Device {device} is not paired to profile {Profile.Name}!");

            foreach (var bindings in _bindsByGameMode.Values)
            {
                bindings.ProcessInputEvent(eventPtr);
            }
        }
    }
}