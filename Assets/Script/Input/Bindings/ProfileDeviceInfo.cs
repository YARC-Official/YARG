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
using YARG.Input.Bindings;

namespace YARG.Input
{
    public class ProfileDeviceInfo : IDisposable
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

        public List<InputDevice> Controllers => _controllers;

        private readonly List<SerializedInputDevice> _unresolvedControllers = new();
        private readonly List<InputDevice> _controllers = new();

        private readonly Dictionary<GameMode, BindingCollection> _bindsByGameMode = new();
        private readonly Dictionary<string, BindingCollection> _bindsByDeviceHash = new();
        public readonly BindingCollection MenuBindings;

        public bool HasDeviceAssigned => _controllers.Count > 0;
        public bool HasMicrophoneAssigned => _microphones.Count == 0;
        public bool HasNoDevices => !HasDeviceAssigned && !HasMicrophoneAssigned;

        public BindingCollection this[GameMode mode] => _bindsByGameMode[mode];

        public event Action<InputDevice> ControllerAdded;
        public event Action<InputDevice> ControllerRemoved;

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

        public ProfileDeviceInfo(YargProfile profile)
        {
            Profile = profile;

            foreach (var mode in EnumExtensions<GameMode>.Values)
            {
                _bindsByGameMode.Add(mode, BindingCollection.CreateGameplayBindings(mode));
            }

            MenuBindings = BindingCollection.CreateMenuBindings();
        }

#nullable enable
        public ProfileDeviceInfo(YargProfile profile, SerializedProfileDeviceInfo? profileBindings)
            : this(profile)
        {
            if (profileBindings is null)
                return;

            if (profileBindings.Controllers is not null)
            {
                foreach (var device in profileBindings.Controllers)
                {
                    if (device is null || string.IsNullOrEmpty(device.Layout) || string.IsNullOrEmpty(device.Hash))
                    {
                        YargLogger.LogFormatWarning("Encountered invalid device entry in bindings for profile {0}!", profile.Name);
                        continue;
                    }

                    // Devices will be resolved later
                    _unresolvedControllers.Add(device);
                }
            }

            if (profileBindings.Microphones.Count > 0)
            {
                foreach (var mic in profileBindings.Microphones)
                {
                    if (mic is not null)
                    {
                        _unresolvedMics.Add(mic);
                    }
                }
            }
            else if (profileBindings.Microphone is not null)
            {
                // Legacy files (v0-v2) only had a single microphone
                _unresolvedMics.Add(profileBindings.Microphone);
            }

            // _bindsByGameMode should currently be populated with the default bindings for each GameMode.
            // If this profile has defined its own GameMode-level binding preferences, we'll overwrite the corresponding
            // entry for each defined mode
            if (profileBindings.ModeMappings is not null)
            {
                List<GameMode> modesToRemove = new();
                foreach (var (mode, bindingSetGuid) in profileBindings.ModeMappings)
                {
                    if (!_bindsByGameMode.TryGetValue(mode, out var modeBindings))
                    {
                        YargLogger.LogFormatWarning("Encountered invalid game mode {0} in bindings for profile {1}!", mode, item2: profile.Name);
                        continue;
                    }

                    if (BindingsContainer.TryGetBindingCollectionById(bindingSetGuid, out var modeMapping))
                    {
                        _bindsByGameMode[mode] = modeMapping;
                    }
                    else
                    {
                        YargLogger.LogWarning($"Referenced nonexistent binding collection GUID {bindingSetGuid}; it will be removed");
                        modesToRemove.Add(mode);
                    }
                }

                foreach (var mode in modesToRemove)
                {
                    profileBindings.ModeMappings.Remove(mode);
                }
            }

            // Profiles can also define preferred binding sets for individual controllers, which supersede GameMode-level preferences.
            // Populate _bindsByDeviceHash with each of those binding sets, regardless of whether the relevant device is currently attached
            // to this profile; if the player attaches it later, we'll want to retrieve their preferred binding set
            if (profileBindings.ControllerMappings is not null)
            {
                List<string> hashesToRemove = new();

                foreach (var (controllerHash, bindingSetGuid) in profileBindings.ControllerMappings)
                {
                    if (BindingsContainer.TryGetBindingCollectionById(bindingSetGuid, out var controllerMapping))
                    {
                        _bindsByDeviceHash[controllerHash] = controllerMapping;
                    }
                    else
                    {
                        YargLogger.LogWarning($"Referenced nonexistent binding collection GUID {bindingSetGuid}; it will be removed");
                        hashesToRemove.Add(controllerHash);
                    }
                }

                foreach (var hash in hashesToRemove)
                {
                    profileBindings.ControllerMappings.Remove(hash);
                }
            }

            if (profileBindings.MenuMapping is not null)
            {
                if (BindingsContainer.TryGetBindingCollectionById(profileBindings.MenuMapping.Value, out var menuMapping))
                {
                    MenuBindings = menuMapping;
                }
                else
                {
                    YargLogger.LogWarning($"Referenced nonexistent binding collection GUID {profileBindings.MenuMapping}; removing it");
                    profileBindings.MenuMapping = null;
                }
            }
        }

        public SerializedProfileDeviceInfo Serialize()
        {
            var serialized = new SerializedProfileDeviceInfo();

            foreach (var device in _controllers)
            {
                serialized.Controllers.Add(device.Serialize());
            }

            foreach (var device in _unresolvedControllers)
            {
                serialized.Controllers.Add(device);
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

                serialized.ModeMappings.Add(mode, serializedBinds.Guid);
            }

            serialized.MenuMapping = MenuBindings.Guid;

            return serialized;
        }

        public static ProfileDeviceInfo Deserialize(YargProfile profile, SerializedProfileDeviceInfo? serialized)
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
                _unresolvedControllers.RemoveAt(index);

            // Add device to bindings
            _controllers.Add(device);
            NotifyDeviceAdded(device);

            return true;
        }

        public bool RemoveDevice(InputDevice device)
        {
            // Remove without serializing
            if (!_controllers.Remove(device))
                return false;

            NotifyDeviceRemoved(device);
            return true;
        }

        public bool ContainsDevice(InputDevice device)
        {
            return _controllers.Contains(device);
        }

        public List<T> GetDevicesByType<T>()
        {
            var interfaces = new List<T>();
            foreach (var device in _controllers)
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
            return _unresolvedControllers.FindIndex((dev) => dev.MatchesDevice(device));
        }

        public bool MatchesDevice(InputDevice device)
        {
            return _unresolvedControllers.Any(dev => dev.MatchesDevice(device));
        }
        public bool ContainsBindingsForDevice(InputDevice device)
        {
            return _bindsByDeviceHash.ContainsKey(device.GetHash());

            // return MenuBindings.ContainsBindingsForDevice(device); TODO: Delete?
        }

        public void ClearBindingsForDevice(InputDevice device, bool clearMenuBindings = true)
        {
            _bindsByDeviceHash.Remove(device.GetHash());

            if (clearMenuBindings)
            {
                // MenuBindings.ClearBindingsForDevice(device); TODO: Delete?
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

            _unresolvedControllers.RemoveAt(serializedIndex);
            _controllers.Add(device);
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

            _controllers.Remove(device);
            _unresolvedControllers.Add(device.Serialize());
            NotifyDeviceRemoved(device);
        }

        private void NotifyDeviceAdded(InputDevice controller)
        {
            var controllerHash = controller.GetHash();

            if (_bindsByDeviceHash.TryGetValue(controller.GetHash(), out var deviceSpecificBindings))
            {
                deviceSpecificBindings.OnDeviceAdded(controller);
            }
            else if (BindingsContainer.TryGetDefaultBindingCollectionForControllerHash(controllerHash, out var deviceDefaultBindings))
            {
                deviceDefaultBindings.OnDeviceAdded(controller);
            }
            else
            {
                foreach (var bindings in _bindsByGameMode.Values)
                {
                    bindings.OnDeviceAdded(controller);
                }
            }

            MenuBindings.OnDeviceAdded(controller);

            ControllerAdded?.Invoke(controller);
        }

        private void NotifyDeviceRemoved(InputDevice device)
        {
            foreach (var bindings in _bindsByGameMode.Values)
            {
                bindings.OnDeviceRemoved(device);
            }

            MenuBindings.OnDeviceRemoved(device);

            ControllerRemoved?.Invoke(device);
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
