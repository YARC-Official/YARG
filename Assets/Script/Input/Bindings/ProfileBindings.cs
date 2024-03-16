using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using YARG.Audio;
using YARG.Core;
using YARG.Core.Audio;
using YARG.Core.Extensions;
using YARG.Core.Game;
using YARG.Core.Logging;
using YARG.Input.Serialization;
using YARG.Player;

namespace YARG.Input
{
    public class ProfileBindings : IDisposable
    {
        public YargProfile Profile { get; }

        private SerializedMic _unresolvedMic;
        public MicDevice Microphone { get; private set; }

        private readonly List<SerializedInputDevice> _unresolvedDevices = new();
        private readonly List<InputDevice> _devices = new();

        private readonly Dictionary<GameMode, BindingCollection> _bindsByGameMode = new();
        public readonly BindingCollection MenuBindings;

        public bool Empty => _devices.Count < 1 && Microphone is null;

        public BindingCollection this[GameMode mode] => _bindsByGameMode[mode];

        public event Action<InputDevice> DeviceAdded;
        public event Action<InputDevice> DeviceRemoved;

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

#nullable enable
        public ProfileBindings(YargProfile profile, SerializedProfileBindings? bindings)
            : this(profile)
        {
            if (bindings is null)
                return;

            if (bindings.Devices is not null)
            {
                foreach (var device in bindings.Devices)
                {
                    if (device is null || string.IsNullOrEmpty(device.Layout) || string.IsNullOrEmpty(device.Hash))
                    {
                        YargLogger.LogFormatWarning("Encountered invalid device entry in bindings for profile {0}!", profile.Name);
                        continue;
                    }

                    // Devices will be resolved later
                    _unresolvedDevices.Add(device);
                }
            }

            _unresolvedMic = bindings.Microphone;

            if (bindings.ModeMappings is not null)
            {
                foreach (var (mode, serializedBinds) in bindings.ModeMappings)
                {
                    if (!_bindsByGameMode.TryGetValue(mode, out var modeBindings))
                    {
                        YargLogger.LogFormatWarning("Encountered invalid game mode {0} in bindings for profile {1}!", mode, item2: profile.Name);
                        continue;
                    }

                    modeBindings.Deserialize(serializedBinds);
                }
            }

            MenuBindings.Deserialize(bindings.MenuMappings);
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

            serialized.Microphone = _unresolvedMic;

            foreach (var (mode, bindings) in _bindsByGameMode)
            {
                var serializedBinds = bindings.Serialize();
                if (serializedBinds is null)
                    continue;

                serialized.ModeMappings.Add(mode, serializedBinds);
            }

            serialized.MenuMappings = MenuBindings.Serialize();

            return serialized;
        }

        public static ProfileBindings Deserialize(YargProfile profile, SerializedProfileBindings? serialized)
        {
            return new(profile, serialized);
        }
#nullable disable

        public void ResolveDevices()
        {
            foreach (var device in InputSystem.devices)
            {
                if (!PlayerContainer.IsDeviceTaken(device))
                    OnDeviceAdded(device);
            }

            if (_unresolvedMic is not null)
            {
                var device = AudioManager.Instance.GetInputDevice(_unresolvedMic.Name);
                if (device != null)
                {
                    AddMicrophone(device);
                }
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

            // Add device to bindings
            _devices.Add(device);
            NotifyDeviceAdded(device);

            // Assign default binds if this device isn't serialized
            if (index < 0)
                SetDefaultBinds(device);

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

        public List<T> GetDevicesByType<T>()
        {
            var interfaces = new List<T>();
            foreach (var device in _devices)
            {
                if (device is T iface)
                {
                    interfaces.Add(iface);
                }
            }

            return interfaces;
        }

        private int FindSerializedIndex(InputDevice device)
        {
            return _unresolvedDevices.FindIndex((dev) => dev.MatchesDevice(device));
        }

        public void ClearBindingsForDevice(InputDevice device)
        {
            foreach (var bindings in _bindsByGameMode.Values)
            {
                bindings.ClearBindingsForDevice(device);
            }

            MenuBindings.ClearBindingsForDevice(device);
        }

        public void ClearAllBindings()
        {
            foreach (var bindings in _bindsByGameMode.Values)
            {
                bindings.ClearAllBindings();
            }

            MenuBindings.ClearAllBindings();
        }

        public bool SetDefaultBinds(InputDevice device)
        {
            if (!ContainsDevice(device))
                return false;

            foreach (var bindings in _bindsByGameMode.Values)
                bindings.SetDefaultBindings(device);

            MenuBindings.SetDefaultBindings(device);

            return true;
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

            DeviceAdded?.Invoke(device);
        }

        private void NotifyDeviceRemoved(InputDevice device)
        {
            foreach (var bindings in _bindsByGameMode.Values)
            {
                bindings.OnDeviceRemoved(device);
            }

            MenuBindings.OnDeviceRemoved(device);

            DeviceRemoved?.Invoke(device);
        }

        public void UpdateBindingsForFrame(double updateTime)
        {
            foreach (var bindings in _bindsByGameMode.Values)
            {
                bindings.UpdateBindingsForFrame(updateTime);
            }

            MenuBindings.UpdateBindingsForFrame(updateTime);
        }

        public bool AddMicrophone(MicDevice microphone)
        {
            if (Microphone is not null)
            {
                microphone.Dispose();
                return false;
            }

            Microphone = microphone;
            _unresolvedMic = microphone.Serialize();

            return true;
        }

        public void RemoveMicrophone()
        {
            Microphone?.Dispose();
            Microphone = null;
            _unresolvedMic = null;
        }

        public void Dispose()
        {
            foreach (var device in InputSystem.devices)
            {
                OnDeviceRemoved(device);
            }

            RemoveMicrophone();
        }
    }
}