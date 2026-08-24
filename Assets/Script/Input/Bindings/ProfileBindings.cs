using System;
using System.Collections.Generic;
using System.Linq;
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

        private readonly List<SerializedMic> _unresolvedMics = new();
        private readonly List<MicDevice> _microphones = new();

        /// <summary>
        ///     The first microphone, for gameplay that only supports one at a time.
        /// </summary>
        public MicDevice Microphone => _microphones.Count > 0 ? _microphones[0] : null;

        /// <summary>
        ///     Every microphone assigned to this profile.
        /// </summary>
        public List<MicDevice> Microphones => _microphones;

        public List<InputDevice> InputDevices => _devices;

        private readonly List<SerializedInputDevice> _unresolvedDevices = new();
        private readonly List<InputDevice> _devices = new();

        private readonly Dictionary<GameMode, BindingCollection> _bindsByGameMode = new();
        public readonly BindingCollection MenuBindings;

        public bool HasDeviceAssigned => _devices.Count > 0;
        public bool Empty => !HasDeviceAssigned && _microphones.Count == 0;

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

            if (bindings.Microphones.Count > 0)
            {
                foreach (var mic in bindings.Microphones)
                {
                    if (mic is not null)
                    {
                        _unresolvedMics.Add(mic);
                    }
                }
            }
            else if (bindings.Microphone is not null)
            {
                // Legacy files (v0-v2) only had a single microphone
                _unresolvedMics.Add(bindings.Microphone);
            }

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

            foreach (var mic in _microphones)
            {
                serialized.Microphones.Add(mic.Serialize());
            }

            foreach (var mic in _unresolvedMics)
            {
                serialized.Microphones.Add(mic);
            }

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

            ResolveMicrophones();
        }

        public void ResolveMicrophones()
        {
            for (int i = _unresolvedMics.Count - 1; i >= 0; i--)
            {
                var mic = _unresolvedMics[i];
                var device = GlobalAudioHandler.GetInputDevice(mic.BaseName, mic.Channel);
                if (device != null)
                {
                    _unresolvedMics.RemoveAt(i);
                    AddMicrophone(device);
                }
            }
        }

        public void ReleaseMicrophones()
        {
            foreach (var mic in _microphones)
            {
                _unresolvedMics.Add(mic.Serialize());
                mic.Dispose();
            }

            _microphones.Clear();
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

        public bool MatchesDevice(InputDevice device)
        {
            return _unresolvedDevices.Any(dev => dev.MatchesDevice(device));
        }

        public bool ContainsBindingsForDevice(InputDevice device)
        {
            foreach (var bindings in _bindsByGameMode.Values)
            {
                if (bindings.ContainsBindingsForDevice(device))
                    return true;
            }

            return MenuBindings.ContainsBindingsForDevice(device);
        }

        public void ClearBindingsForDevice(InputDevice device, bool clearMenuBindings = true)
        {
            foreach (var bindings in _bindsByGameMode.Values)
            {
                bindings.ClearBindingsForDevice(device);
            }

            if (clearMenuBindings)
            {
                MenuBindings.ClearBindingsForDevice(device);
            }
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
            {
                return false;
            }

            foreach (var bindings in _bindsByGameMode.Values)
            {
                bindings.SetDefaultBindings(device);
            }

            MenuBindings.SetDefaultBindings(device);

            return true;
        }

        public bool SetDefaultBinds(Gamepad gamepad, GamepadBindingMode mode)
        {
            if (!ContainsDevice(gamepad))
            {
                return false;
            }

            foreach (var bindings in _bindsByGameMode.Values)
            {
                bindings.SetDefaultBindings(gamepad, mode);
            }

            MenuBindings.SetDefaultBindings(gamepad, mode);

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

        public void AddMicrophone(MicDevice microphone)
        {
            if (_microphones.Contains(microphone))
            {
                return;
            }

            _microphones.Add(microphone);

            var serialized = microphone.Serialize();
            for (int i = _unresolvedMics.Count - 1; i >= 0; i--)
            {
                if (_unresolvedMics[i].BaseName == serialized.BaseName && _unresolvedMics[i].Channel == serialized.Channel)
                {
                    _unresolvedMics.RemoveAt(i);
                }
            }
        }

        public void RemoveMicrophone(MicDevice microphone)
        {
            if (!_microphones.Remove(microphone))
            {
                return;
            }

            microphone.Dispose();
        }

        public void RemoveAllMicrophones()
        {
            foreach (var mic in _microphones)
            {
                mic.Dispose();
            }

            _microphones.Clear();
            _unresolvedMics.Clear();
        }

        public void Dispose()
        {
            foreach (var device in InputSystem.devices)
            {
                OnDeviceRemoved(device);
            }

            ReleaseMicrophones();
        }
    }
}
