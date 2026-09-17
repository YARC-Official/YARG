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
using UnityEngine.InputSystem.Utilities;
using YARG.Menu.ProfileList;
using YARG.Helpers;
using FuzzySharp.Utils;

namespace YARG.Input
{
    public class PlayerDeviceInfo : IDisposable
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
        private bool _inputsEnabled;

        private readonly List<SerializedInputDevice> _unresolvedControllers = new();
        private readonly List<InputDevice> _controllers = new();
        private readonly Dictionary<InputDevice, RuntimeBindingSet> _activeGameplayBindings = new();
        private readonly Dictionary<InputDevice, RuntimeBindingSet> _activeMenuBindings = new();
        private readonly Dictionary<(GameMode mode, ControllerFamily controllerFamily), ReusableBindingSet> _preferredBindsByContext = new();
        public readonly Dictionary<ControllerFamily, ReusableBindingSet> PreferredMenuBindingsByBaseLayout = new();

        public bool HasDeviceAssigned => _controllers.Count > 0;
        public bool HasMicrophoneAssigned => _microphones.Count == 0;
        public bool HasNoDevices => !HasDeviceAssigned && !HasMicrophoneAssigned;

        public event Action<InputDevice> ControllerAdded;
        public event Action<InputDevice> ControllerRemoved;

        /* TODO-FRICK: 
         * Because we no longer edit runtime bindings directly, I don't think we need this to work like this.
         * There will probably still be public event Action BindingsChanged, but I think it'll just reload from
         * a now-updated ReusableBindingSet all over again. Or maybe that's inefficient and we do something smarter,
         * but in any case I think this particular code is on its way out. Preserving for now
         * 
        public event Action BindingsChanged
        {
            add
            {
                foreach (var binds in _activeGameplayBindings.Values)
                {
                    binds.BindingsChanged += value;
                }

                foreach (var layoutMenuBinds in _activeMenuBindings.Values)
                {
                    layoutMenuBinds.BindingsChanged += value;
                }
            }
            remove
            {
                foreach (var binds in _activeGameplayBindings.Values)
                {
                    binds.BindingsChanged -= value;
                }

                foreach (var layoutMenuBinds in _activeMenuBindings.Values)
                {
                    layoutMenuBinds.BindingsChanged -= value;
                }
            }
        }*/

        
        public event GameInputProcessed MenuInputProcessed
        {
            add
            {
                foreach (var menuBinds in _activeMenuBindings.Values)
                {
                    menuBinds.InputProcessed += value;
                }
            }    
            remove
            {
                foreach (var menuBinds in _activeMenuBindings.Values)
                {
                    menuBinds.InputProcessed -= value;
                }
            }
        }

        public PlayerDeviceInfo() { }

        public PlayerDeviceInfo(YargProfile profile)
        {
            Profile = profile;
        }

#nullable enable
        public PlayerDeviceInfo(YargProfile profile, SerializedPlayerDeviceInfo? serialized) : this(profile)
        {
            if (serialized is null)
                return;

            if (serialized.Controllers is not null)
            {
                foreach (var device in serialized.Controllers)
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

            if (serialized.Microphones.Count > 0)
            {
                foreach (var mic in serialized.Microphones)
                {
                    if (mic is not null)
                    {
                        _unresolvedMics.Add(mic);
                    }
                }
            }
            else if (serialized.Microphone is not null)
            {
                // Legacy files (v0-v2) only had a single microphone
                _unresolvedMics.Add(serialized.Microphone);
            }

            if (serialized.ModeMappings is not null)
            {
                List<GameMode> modesToRemove = new();
                foreach (var (mode, modeMappings) in serialized.ModeMappings)
                {
                    foreach (var (baseLayout, bindings) in modeMappings.MappingsByBaseLayout)
                    {
                        var controllerFamily = LayoutHelper.LayoutStringToControllerFamily(baseLayout);

                        if (modeMappings.MappingsByBaseLayout.TryGetValue(baseLayout, out var bindingSetGuid))
                        {
                            if (BindingsContainer.TryGetBindingCollectionById(bindingSetGuid, out var modeMapping))
                            {
                                _preferredBindsByContext[(mode, controllerFamily)] = modeMapping;
                            }
                            else
                            {
                                YargLogger.LogWarning($"Referenced nonexistent binding collection GUID {bindingSetGuid}; it will be removed");
                                modesToRemove.Add(mode);
                            }
                        }

                    }


                }

                foreach (var mode in modesToRemove)
                {
                    serialized.ModeMappings.Remove(mode);
                }
            }

            if (serialized.MenuMappings is not null)
            {
                foreach (var (baseLayout, bindingSetGuid) in serialized.MenuMappings)
                {
                    var controllerFamily = LayoutHelper.LayoutStringToControllerFamily(baseLayout);

                    if (BindingsContainer.TryGetBindingCollectionById(bindingSetGuid, out var menuMapping))
                    {
                        PreferredMenuBindingsByBaseLayout[controllerFamily] = menuMapping;
                    }
                    else
                    {
                        YargLogger.LogWarning($"Referenced nonexistent binding collection GUID {bindingSetGuid}; removing it");
                        serialized.MenuMappings.Remove(baseLayout);
                    }
                }

            }
        }

        public SerializedPlayerDeviceInfo Serialize()
        {
            var serialized = new SerializedPlayerDeviceInfo();

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

            foreach (var ((mode, controllerFamily), bindingSet) in _preferredBindsByContext)
            {
                var serializedBinds = bindingSet.Serialize();
                if (serializedBinds is null)
                    continue;

                if (!serialized.ModeMappings.ContainsKey(mode))
                {
                    serialized.ModeMappings[mode] = new();
                }

                var baseLayout = LayoutHelper.ControllerFamilyToLayoutString(controllerFamily);
                serialized.ModeMappings[mode].MappingsByBaseLayout[baseLayout] = bindingSet.Guid;
            }

            foreach (var (controllerFamily, bindingSet) in PreferredMenuBindingsByBaseLayout)
            {
                var serializedMenuBinds = bindingSet.Serialize();

                if (serializedMenuBinds is null)
                {
                    continue;
                }

                var baseLayout = LayoutHelper.ControllerFamilyToLayoutString(controllerFamily);
                serialized.MenuMappings[baseLayout] = bindingSet.Guid;
            }

            return serialized;
        }

        public static PlayerDeviceInfo Deserialize(YargProfile profile, SerializedPlayerDeviceInfo? serialized)
        {
            return new(profile, serialized);
        }
#nullable disable

        public void ResolveDevices()
        {
            foreach (var device in InputSystem.devices)
            {
                if (!PlayerContainer.IsDeviceTaken(device))
                    OnControllerAdded(device);
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
            _inputsEnabled = true;

            foreach (var bindings in _activeGameplayBindings.Values)
            {
                bindings.EnableInputs();
            }

            foreach (var bindings in _activeMenuBindings.Values)
            {
                bindings.EnableInputs();
            }
        }

        public void DisableInputs()
        {
            _inputsEnabled = false;

            foreach (var bindings in _activeGameplayBindings.Values)
            {
                bindings.DisableInputs();
            }

            foreach (var bindings in _activeMenuBindings.Values)
            {
                bindings.DisableInputs();
            }
        }

        public void SubscribeToGameplayInputs(GameInputProcessed onInputProcessed)
        {
            foreach (var bindings in _activeGameplayBindings.Values)
            {
                bindings.InputProcessed += onInputProcessed;
            }

        }

        public void UnsubscribeFromGameplayInputs(GameInputProcessed onInputProcessed)
        {
            foreach (var bindings in _activeGameplayBindings.Values)
            {
                bindings.InputProcessed -= onInputProcessed;
            }
        }

        public bool AddController(InputDevice controller)
        {
            // Ignore already-added devices
            if (ContainsController(controller))
                return false;

            // Remove corresponding serialized entry
            int index = FindSerializedIndex(controller);
            if (index >= 0)
                _unresolvedControllers.RemoveAt(index);

            // Add device to bindings
            _controllers.Add(controller);
            NotifyControllerAdded(controller);

            return true;
        }

        public bool RemoveController(InputDevice controller)
        {
            // Remove without serializing
            if (!_controllers.Remove(controller))
                return false;

            NotifyControllerRemoved(controller);
            return true;
        }

        public bool ContainsController(InputDevice controller)
        {
            return _controllers.Contains(controller);
        }

        public List<T> GetDevicesByType<T>()
        {
            var interfaces = new List<T>();
            foreach (var controller in _controllers)
            {
                if (controller is T iface)
                {
                    interfaces.Add(iface);
                }
            }

            return interfaces;
        }

        private int FindSerializedIndex(InputDevice controller)
        {
            return _unresolvedControllers.FindIndex((dev) => dev.MatchesDevice(controller));
        }

        public bool MatchesController(InputDevice controller)
        {
            return _unresolvedControllers.Any(dev => dev.MatchesDevice(controller));
        }

        // TODO: Delete?
        public bool ContainsBindingsForController(InputDevice controller)
        {
            return false;

            //return _bindsByDeviceHash.ContainsKey(controller.GetHash());

            // return MenuBindings.ContainsBindingsForDevice(device); TODO: Delete?
        }

        /* TODO: After controller overrides are implemented
        public void ClearBindingsForController(InputDevice controller, bool clearMenuBindings = true)
        {
            _bindsByDeviceHash.Remove(controller.GetHash());

            if (clearMenuBindings)
            {
                // MenuBindings.ClearBindingsForDevice(device); TODO: Delete?
            }
        }
        */

        public bool SetDefaultBinds(InputDevice controller)
        {
            if (!ContainsController(controller))
            {
                return false;
            }

            foreach (var bindings in _preferredBindsByContext.Values)
            {
                // bindings.SetDefaultBindings(controller); TODO
            }

            foreach (var bindings in PreferredMenuBindingsByBaseLayout.Values)
            {
                // bindings.SetDefaultBindings(controller); TODO
            }

            return true;
        }

        public bool SetDefaultBinds(Gamepad gamepad, GamepadBindingMode mode)
        {
            if (!ContainsController(gamepad))
            {
                return false;
            }

            foreach (var bindings in _preferredBindsByContext.Values)
            {
                // bindings.SetDefaultBindings(gamepad, mode); TODO
            }

            foreach (var bindings in PreferredMenuBindingsByBaseLayout.Values)
            {
                // bindings.SetDefaultBindings(gamepad, mode); TODO
            }

            return true;
        }

        public void OnControllerAdded(InputDevice controller)
        {
            // Ignore already-added devices
            if (ContainsController(controller))
                return;

            // Ignore devices not registered to this profile
            int serializedIndex = FindSerializedIndex(controller);
            if (serializedIndex < 0)
                return;

            _unresolvedControllers.RemoveAt(serializedIndex);
            _controllers.Add(controller);
            NotifyControllerAdded(controller);
        }

        public void OnControllerRemoved(InputDevice controller)
        {
            // Ignore devices not registered to this profile
            if (!ContainsController(controller))
                return;

            // Ensure devices aren't serialized twice
            int serializedIndex = FindSerializedIndex(controller);
            if (serializedIndex >= 0)
                return;

            _controllers.Remove(controller);
            _unresolvedControllers.Add(controller.Serialize());
            NotifyControllerRemoved(controller);
        }

        public void RefreshGameMode()
        {
            foreach (var bindings in _activeGameplayBindings.Values)
            {
                bindings.Dispose();
            }
            _activeGameplayBindings.Clear();

            foreach (var controller in _controllers)
            {
                var newGameplayBindings = GetCollectionForController(controller, menu: false);
                _activeGameplayBindings[controller] = newGameplayBindings;
                newGameplayBindings.EnableInputs();
            }
        }

        private void NotifyControllerAdded(InputDevice controller)
        {
            var newGameplayBindings = GetCollectionForController(controller, menu: false);
            var newMenuBindings = GetCollectionForController(controller, menu: true);

            _activeGameplayBindings[controller] = newGameplayBindings;
            _activeMenuBindings[controller] = newMenuBindings;

            if (_inputsEnabled)
            {
                newGameplayBindings.EnableInputs();
                newMenuBindings.EnableInputs();
            }

            ControllerAdded?.Invoke(controller);
        }
        
        private void NotifyControllerRemoved(InputDevice controller)
        {
            if (_activeGameplayBindings.Remove(controller, out var gameplayBindings))
            {
                gameplayBindings.Dispose();
            }

            if (_activeMenuBindings.Remove(controller, out var menuBindings))
            {
                menuBindings.Dispose();
            }

            ControllerRemoved?.Invoke(controller);
        }

        private RuntimeBindingSet GetCollectionForController(InputDevice controller, bool menu)
        {
            RuntimeBindingSet runtimeBindings;
            var family = LayoutHelper.InputDeviceToControllerFamily(controller);
            var mode = menu ? GameMode.Menu : Profile.GameMode;

            // TODO-FRICK: Implement Profile-level device-specific overrides (keyed by name or hash, tbd) to
            // check before general (profile,mode,family)-wide preferred bindings

            if (_preferredBindsByContext.TryGetValue((mode, family), out var preferredBindingSet))
            {
                runtimeBindings = preferredBindingSet.GetRuntimeBindings(Profile, controller);
            }
            else
            {
                var defaults = BindingsContainer.GetBindingSetsForControllerInMode(family, mode);
                if (defaults.Count > 0)
                {
                    runtimeBindings = defaults.First().GetRuntimeBindings(Profile, controller);

                    // TODO-FRICK: Also look for better-match defaults beyond the first, like giving a Riffmaster the "Default Riffmaster
                    // Gameplay" set instead of "Default 5F Guitar Gameplay"
                }
                else
                {
                    runtimeBindings = new(controller, mode);
                }
            }

            return runtimeBindings;
        }
        
        public void UpdateBindingsForFrame(double updateTime)
        {
            foreach (var bindings in _activeGameplayBindings.Values)
            {
                bindings.UpdateBindingsForFrame(updateTime);
            }

            foreach (var bindings in _activeMenuBindings.Values)
            {
                bindings.UpdateBindingsForFrame(updateTime);
            }
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
            foreach (var controller in InputSystem.devices)
            {
                OnControllerRemoved(controller);
            }

            ReleaseMicrophones();
        }

# nullable enable
        public ReusableBindingSet? GetPreferredBindingSet(GameMode mode, ControllerFamily controllerFamily)
        {
            return _preferredBindsByContext.GetValueOrDefault((mode, controllerFamily), null);
        }
    }
}
