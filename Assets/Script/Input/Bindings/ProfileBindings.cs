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
    public class ProfileBindings : IDisposable
    {
        public YargProfile Profile { get; }

        private readonly List<SerializedInputDevice> _unresolvedDevices = new();
        private readonly List<InputDevice> _devices = new();

        private readonly Dictionary<GameMode, BindingCollection> _bindsByGameMode = new();
        public readonly BindingCollection MenuBindings;

        public bool Empty => _devices.Count < 1;

        public BindingCollection this[GameMode mode] => _bindsByGameMode[mode];

        public event Action BindingsChanged
        {
            add
            {
                foreach (var bindings in _bindsByGameMode.Values)
                {
                    bindings.BindingsChanged += value;
                }

                MenuBindings.BindingsChanged += value;
            }
            remove
            {
                foreach (var bindings in _bindsByGameMode.Values)
                {
                    bindings.BindingsChanged -= value;
                }

                MenuBindings.BindingsChanged -= value;
            }
        }

        public event GameInputProcessed MenuInputProcessed
        {
            add    => MenuBindings.InputProcessed += value;
            remove => MenuBindings.InputProcessed -= value;
        }

        public ProfileBindings(YargProfile profile)
        {
            Profile = profile;

            foreach (var mode in EnumExtensions<GameMode>.Values)
            {
                _bindsByGameMode.Add(mode, BindingCollection.CreateGameplayBindings(mode));
            }

            MenuBindings = BindingCollection.CreateMenuBindings();
        }

        public ProfileBindings(YargProfile profile, SerializedProfileBindings bindings)
            : this(profile)
        {
            foreach (var device in bindings.Devices)
            {
                // Devices will be resolved later
                _unresolvedDevices.Add(device);
            }

            foreach (var (mode, serializedBinds) in bindings.Bindings)
            {
                if (!_bindsByGameMode.TryGetValue(mode, out var modeBindings))
                {
                    Debug.LogWarning($"Encountered invalid game mode {mode}!");
                    continue;
                }

                modeBindings.Deserialize(serializedBinds);
            }

            MenuBindings.Deserialize(bindings.MenuBindings);
        }

        public SerializedProfileBindings Serialize()
        {
            var serialized = new SerializedProfileBindings();

            foreach (var device in _devices)
            {
                serialized.Devices.Add(device.Serialize());
            }

            foreach (var device in _unresolvedDevices)
            {
                serialized.Devices.Add(device);
            }

            foreach (var (mode, bindings) in _bindsByGameMode)
            {
                serialized.Bindings.Add(mode, bindings.Serialize());
            }

            serialized.MenuBindings = MenuBindings.Serialize();

            return serialized;
        }

        public static ProfileBindings Deserialize(YargProfile profile, SerializedProfileBindings serialized)
        {
            return new(profile, serialized);
        }

        public void ResolveDevices()
        {
            foreach (var device in InputSystem.devices)
            {
                OnDeviceAdded(device);
            }
        }

        public void EnableInputs()
        {
            foreach (var bindings in _bindsByGameMode.Values)
            {
                bindings.EnableInputs();
            }

            MenuBindings.EnableInputs();
        }

        public void DisableInputs()
        {
            foreach (var bindings in _bindsByGameMode.Values)
            {
                bindings.DisableInputs();
            }

            MenuBindings.DisableInputs();
        }

        public void SubscribeToGameplayInputs(GameMode mode, GameInputProcessed onInputProcessed)
        {
            _bindsByGameMode[mode].InputProcessed += onInputProcessed;
        }

        public void UnsubscribeFromGameplayInputs(GameMode mode, GameInputProcessed onInputProcessed)
        {
            _bindsByGameMode[mode].InputProcessed -= onInputProcessed;
        }

        public bool AddDevice(InputDevice device)
        {
            // Ignore already-added devices
            if (ContainsDevice(device))
                return false;

            // Remove corresponding serialized entry
            int index = FindSerializedIndex(device);
            if (index >= 0)
                _unresolvedDevices.RemoveAt(index);

            _devices.Add(device);
            NotifyDeviceAdded(device);
            return true;
        }

        public bool RemoveDevice(InputDevice device)
        {
            // Remove without serializing
            if (!_devices.Remove(device))
                return false;

            NotifyDeviceRemoved(device);
            return true;
        }

        public bool ContainsDevice(InputDevice device)
        {
            return _devices.Contains(device);
        }

        private int FindSerializedIndex(InputDevice device)
        {
            return _unresolvedDevices.FindIndex((dev) => dev.MatchesDevice(device));
        }

        public void OnDeviceAdded(InputDevice device)
        {
            // Ignore already-added devices
            if (ContainsDevice(device))
                return;

            // Ignore devices not registered to this profile
            int serializedIndex = FindSerializedIndex(device);
            if (serializedIndex < 0)
                return;

            _unresolvedDevices.RemoveAt(serializedIndex);
            _devices.Add(device);
            NotifyDeviceAdded(device);
        }

        public void OnDeviceRemoved(InputDevice device)
        {
            // Ignore devices not registered to this profile
            if (!ContainsDevice(device))
                return;

            // Ensure devices aren't serialized twice
            int serializedIndex = FindSerializedIndex(device);
            if (serializedIndex >= 0)
                return;

            _devices.Remove(device);
            _unresolvedDevices.Add(device.Serialize());
            NotifyDeviceRemoved(device);
        }

        private void NotifyDeviceAdded(InputDevice device)
        {
            foreach (var bindings in _bindsByGameMode.Values)
            {
                bindings.OnDeviceAdded(device);
            }

            MenuBindings.OnDeviceAdded(device);
        }

        private void NotifyDeviceRemoved(InputDevice device)
        {
            foreach (var bindings in _bindsByGameMode.Values)
            {
                bindings.OnDeviceRemoved(device);
            }

            MenuBindings.OnDeviceRemoved(device);
        }

        public void ProcessInputEvent(InputEventPtr eventPtr)
        {
            var device = InputSystem.GetDeviceById(eventPtr.deviceId);
            if (!ContainsDevice(device))
            {
                throw new InvalidOperationException($"Device {device} is not paired to profile {Profile.Name}!");
            }

            foreach (var bindings in _bindsByGameMode.Values)
            {
                bindings.ProcessInputEvent(eventPtr);
            }

            MenuBindings.ProcessInputEvent(eventPtr);
        }

        public void Dispose()
        {
            foreach (var device in InputSystem.devices)
            {
                OnDeviceRemoved(device);
            }
        }
    }
}