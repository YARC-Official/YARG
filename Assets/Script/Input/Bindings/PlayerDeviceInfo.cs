using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using YARG.Core;
using YARG.Core.Audio;
using YARG.Core.Game;
using YARG.Core.Logging;
using YARG.Helpers;
using YARG.Input.Bindings;
using YARG.Input.Serialization;
using YARG.Menu.ProfileList;
using YARG.Player;

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
        private readonly Dictionary<InputDevice, ReusableBindingSet> _selectedGameplayBindings = new();
        private readonly Dictionary<InputDevice, RuntimeBindingSet> _activeGameplayRuntimeBindings = new();
        private readonly Dictionary<InputDevice, RuntimeBindingSet> _activeMenuRuntimeBindings = new();
        private readonly Dictionary<(GameMode mode, ControllerFamily controllerFamily), ReusableBindingSet> _preferredBindsByContext = new();
        public IEnumerable<ReusableBindingSet> AllPreferredBindingSets => _preferredBindsByContext.Values;
        public IEnumerable<ReusableBindingSet> BindingSetsInUse {
            get
            {
                var list = _selectedGameplayBindings.Values.ToList();
                foreach (var activeMenuBinding in _activeMenuRuntimeBindings.Values)
                {
                    list.Add(activeMenuBinding.Source);
                }
                return list;
            }
        }

        private readonly RuntimeInputAggregator _gameplayInputAggregator = new();
        private readonly RuntimeInputAggregator _menuInputAggregator = new();

        public bool HasDeviceAssigned => _controllers.Count > 0;
        public bool HasMicrophoneAssigned => _microphones.Count == 0;
        public bool HasNoDevices => !HasDeviceAssigned && !HasMicrophoneAssigned;

        public event Action<InputDevice> ControllerAdded;
        public event Action<InputDevice> ControllerRemoved;
       

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
                if (!PlayerContainer.IsControllerTaken(device))
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

            foreach (var bindings in _activeGameplayRuntimeBindings.Values)
            {
                bindings.EnableInputs();
            }

            foreach (var bindings in _activeMenuRuntimeBindings.Values)
            {
                bindings.EnableInputs();
            }
        }

        public void DisableInputs()
        {
            _inputsEnabled = false;

            foreach (var bindings in _activeGameplayRuntimeBindings.Values)
            {
                bindings.DisableInputs();
            }

            foreach (var bindings in _activeMenuRuntimeBindings.Values)
            {
                bindings.DisableInputs();
            }
        }

        public void SubscribeToGameplayInputs(GameInputProcessed onInputProcessed)
        {
            _gameplayInputAggregator.InputProcessed += onInputProcessed;
        }

        public void UnsubscribeFromGameplayInputs(GameInputProcessed onInputProcessed)
        {
            _gameplayInputAggregator.InputProcessed -= onInputProcessed;
        }

        public void SubscribeToMenuInputs(GameInputProcessed onInputProcessed)
        {
            _menuInputAggregator.InputProcessed += onInputProcessed;
        }

        public void UnsubscribeFromMenuInputs(GameInputProcessed onInputProcessed)
        {
            _menuInputAggregator.InputProcessed -= onInputProcessed;
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

            _selectedGameplayBindings.Remove(controller);

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

        public void SetGameplayBindingsForController(InputDevice controller, ReusableBindingSet bindingSet)
        {
            if (bindingSet is null)
            {
                _selectedGameplayBindings.Remove(controller);
                _preferredBindsByContext.Remove((Profile.GameMode, LayoutHelper.InputDeviceToControllerFamily(controller)));
            }
            else
            {
                _selectedGameplayBindings[controller] = bindingSet;
                _preferredBindsByContext[(Profile.GameMode, LayoutHelper.InputDeviceToControllerFamily(controller))] = bindingSet;
            }            
        }

        public ReusableBindingSet GetSelectedGameplayBindingsForController(InputDevice controller)
        {
            return _selectedGameplayBindings.GetValueOrDefault(controller, null);
        }

        public void SetActiveMenuBindingsForController(InputDevice controller, ReusableBindingSet bindingSet)
        {
            if (_activeMenuRuntimeBindings.Remove(controller, out var oldRuntimeBindings))
            {
                oldRuntimeBindings.Source.Changed -= OnMenuBindingSetChanged;
                _menuInputAggregator.Remove(oldRuntimeBindings);
                oldRuntimeBindings.Dispose();
            }

            if (bindingSet is null)
            {
                return;
            }

            bindingSet.Changed += OnMenuBindingSetChanged;

            var newRuntimeBindings = bindingSet.GetRuntimeBindings(Profile, controller);

            _activeMenuRuntimeBindings[controller] = newRuntimeBindings;
            _menuInputAggregator.Add(newRuntimeBindings);

            if (_inputsEnabled)
            {
                newRuntimeBindings.EnableInputs();
            }
        }

        public ReusableBindingSet GetActiveMenuBindingsForController(InputDevice controller)
        {
            return _activeMenuRuntimeBindings.GetValueOrDefault(controller, null)?.Source;
        }

        public void ActivateGameplayBindings()
        {
            foreach (var existingRuntimeBindings in _activeGameplayRuntimeBindings.Values)
            {
                _gameplayInputAggregator.Remove(existingRuntimeBindings);
                existingRuntimeBindings.Dispose();
            }

            _activeGameplayRuntimeBindings.Clear();

            foreach (var controller in _controllers)
            {
                if (!_selectedGameplayBindings.TryGetValue(controller, out var bindingSet))
                {
                    continue;
                }

                var runtimeBindings = bindingSet.GetRuntimeBindings(Profile, controller);

                _activeGameplayRuntimeBindings[controller] = runtimeBindings;
                _gameplayInputAggregator.Add(runtimeBindings);

                if (_inputsEnabled)
                {
                    runtimeBindings.EnableInputs();
                }
            }
        }

        private int FindSerializedIndex(InputDevice controller)
        {
            return _unresolvedControllers.FindIndex((dev) => dev.MatchesDevice(controller));
        }

        public bool MatchesController(InputDevice controller)
        {
            return _unresolvedControllers.Any(dev => dev.MatchesDevice(controller));
        }

        public (ReusableBindingSet gameplay, ReusableBindingSet menu) GetBindingSetsForGamepad(GamepadBindingMode bindingMode, GameMode gameMode)
        {
            ReusableBindingSet gameplay = null;
            ReusableBindingSet menu = null;

            // We'll still honor an explicit preference if it exists, but our fallback is the provided
            // default rather than just whatever's listed first in the dictionary's list. If the provided
            // default is null, then we do fall back to that as usual
            ReusableBindingSet GetPreferenceOrDefault(GameMode mode, ReusableBindingSet @default)
            {
                return _preferredBindsByContext.GetValueOrDefault(
                    (mode, ControllerFamily.Gamepad),
                    @default ?? GetBindingSetForControllerFamily(ControllerFamily.Gamepad, mode is GameMode.Menu)
                );
            }

            switch (bindingMode)
            {
                case GamepadBindingMode.Gamepad:
                    gameplay = GetBindingSetForControllerFamily(ControllerFamily.Gamepad, false);
                    menu = GetBindingSetForControllerFamily(ControllerFamily.Gamepad, true);
                    break;
                case GamepadBindingMode.CrkdGuitar_Mode1:
                    gameplay = GetPreferenceOrDefault(gameMode, gameMode is GameMode.FiveFretGuitar ? ReusableBindingSetDefaults.DefaultCrkdMode1Gameplay : null);
                    menu = GetPreferenceOrDefault(GameMode.Menu, ReusableBindingSetDefaults.DefaultCrkdMode1Menu);
                    break;
                case GamepadBindingMode.CrkdGuitar_Mode1_Fw30:
                    gameplay = GetPreferenceOrDefault(gameMode, gameMode is GameMode.FiveFretGuitar ? ReusableBindingSetDefaults.DefaultCrkdMode1Fw30Gameplay : null);
                    menu = GetPreferenceOrDefault(GameMode.Menu, ReusableBindingSetDefaults.DefaultCrkdMode1Menu);
                    break;
                case GamepadBindingMode.WiitarThing_Guitar or GamepadBindingMode.RB4InstrumentMapper_Guitar:
                    gameplay = GetPreferenceOrDefault(gameMode, gameMode is GameMode.FiveFretGuitar ? ReusableBindingSetDefaults.DefaultGamepadFiveFretGuitarGameplay : null);
                    menu = GetPreferenceOrDefault(GameMode.Menu, ReusableBindingSetDefaults.DefaultGamepadFiveFretGuitarMenu);
                    break;
                case GamepadBindingMode.WiitarThing_Drums:
                    gameplay = GetPreferenceOrDefault(gameMode, gameMode is GameMode.FiveLaneDrums ? ReusableBindingSetDefaults.DefaultGamepadFiveLaneDrumkitGameplay : null);
                    menu = GetPreferenceOrDefault(GameMode.Menu, ReusableBindingSetDefaults.DefaultGamepadFiveLaneDrumkitMenu);
                    break;
                case GamepadBindingMode.RB4InstrumentMapper_GHLGuitar:
                    gameplay = GetPreferenceOrDefault(gameMode, gameMode is GameMode.SixFretGuitar ? ReusableBindingSetDefaults.DefaultGamepadSixFretGuitarGameplay : null);
                    menu = GetPreferenceOrDefault(GameMode.Menu, ReusableBindingSetDefaults.DefaultGamepadSixFretGuitarMenu);
                    break;
                case GamepadBindingMode.RB4InstrumentMapper_Drums:
                    gameplay = GetPreferenceOrDefault(gameMode, gameMode is GameMode.FourLaneDrums ? ReusableBindingSetDefaults.DefaultGamepadFourLaneDrumkitGameplay : null);
                    menu = GetPreferenceOrDefault(GameMode.Menu, ReusableBindingSetDefaults.DefaultGamepadFourLaneDrumkitMenu);
                    break;
            }

            return (gameplay, menu);
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
            foreach (var controller in _controllers)
            {
                var newGameplayBindings = GetBindingSetForController(controller, menu: false);

                if (newGameplayBindings is null)
                {
                    _selectedGameplayBindings.Remove(controller);
                }
                else
                {
                    _selectedGameplayBindings[controller] = newGameplayBindings;
                }
            }
        }

        public void OnBindingSetDeleted(ReusableBindingSet deleted)
        {
            _preferredBindsByContext.Remove((deleted.Mode, deleted.ControllerFamily));

            // If we deleted a binding set that was being used by an actively-connected player, then
            // we might have some extra work to do, especially if this was a menu binding set since
            // those are always wired up
            if (PlayerContainer.IsProfileTaken(Profile))
            {
                if (deleted.Mode is GameMode.Menu)
                {
                    foreach (var controller in Controllers)
                    {
                        if (_activeMenuRuntimeBindings[controller].Source == deleted)
                        {
                            var layout = LayoutHelper.LayoutStringToControllerFamily(controller.layout);
                            var newMenuBindings = BindingsContainer.GetBindingSetsForControllerInMode(layout, GameMode.Menu).FirstOrDefault();

                            if (newMenuBindings is not null)
                            {
                                SetActiveMenuBindingsForController(controller, newMenuBindings);
                            }
                        }
                    }
                }
                else if (deleted.Mode == Profile.GameMode)
                {
                    foreach (var controller in Controllers)
                    {
                        if (_selectedGameplayBindings[controller] == deleted)
                        {
                            var layout = LayoutHelper.LayoutStringToControllerFamily(controller.layout);
                            var newGameplayBindings = BindingsContainer.GetBindingSetsForControllerInMode(layout, Profile.GameMode).FirstOrDefault();

                            if (newGameplayBindings is not null)
                            {
                                _selectedGameplayBindings[controller] = newGameplayBindings;
                            }
                        }
                    }
                }
            }
        }

        private void NotifyControllerAdded(InputDevice controller)
        {
            if (!_selectedGameplayBindings.ContainsKey(controller))
            {
                var selectedGameplayBindings = GetBindingSetForController(controller, menu: false);
                if (selectedGameplayBindings is not null)
                {
                    _selectedGameplayBindings[controller] = selectedGameplayBindings;
                }
            }

            SetActiveMenuBindingsForController(controller, GetBindingSetForController(controller, menu: true));

            ControllerAdded?.Invoke(controller);
        }
        
        private void NotifyControllerRemoved(InputDevice controller)
        {
            if (_activeGameplayRuntimeBindings.Remove(controller, out var gameplayBindings))
            {
                _gameplayInputAggregator.Remove(gameplayBindings);
                gameplayBindings.Dispose();
            }

            if (_activeMenuRuntimeBindings.Remove(controller, out var menuBindings))
            {
                _menuInputAggregator.Remove(menuBindings);
                menuBindings.Dispose();
            }

            ControllerRemoved?.Invoke(controller);
        }

        public void ActivateMenuBindings()
        {
            foreach (var controller in _controllers)
            {
                if (_activeMenuRuntimeBindings.ContainsKey(controller))
                {
                    continue;
                }

                var menuBindings = GetBindingSetForController(controller, menu: true);
                SetActiveMenuBindingsForController(controller, menuBindings);
            }
        }

        public void SetMenuBindingsForController(InputDevice controller, ReusableBindingSet bindingSet)
        {
            var family = LayoutHelper.LayoutStringToControllerFamily(controller.layout);

            if (bindingSet is null)
            {
                _preferredBindsByContext.Remove((GameMode.Menu, family));
            }
            else
            {
                _preferredBindsByContext[(GameMode.Menu, family)] = bindingSet;
            }

            SetActiveMenuBindingsForController(controller, bindingSet);
        }

        private ReusableBindingSet GetBindingSetForController(InputDevice controller, bool menu)
        {
            var family = LayoutHelper.InputDeviceToControllerFamily(controller);
            var mode = menu ? GameMode.Menu : Profile.GameMode;

            if (_preferredBindsByContext.TryGetValue((mode, family), out var preferredBindingSet))
            {
                return preferredBindingSet;
            }

            var defaults = BindingsContainer.GetBindingSetsForControllerInMode(family, mode);
            if (defaults.Count > 0)
            {
                // Special cases to catch certain controller types - move to a helper if this gets too long
                switch ((family, mode))
                {
                    case (ControllerFamily.FiveFretGuitar, GameMode.FiveFretGuitar):
                        if (controller.name.Contains("Riffmaster"))
                        {
                            return ReusableBindingSetDefaults.DefaultRiffmasterGuitar;
                        }
                        break;
                    case (ControllerFamily.MidiDevice, GameMode.EliteDrums):
                        if (controller.name.Contains("Alesis Nitro") || controller.name.Contains("Alesis Surge"))
                        {
                            return ReusableBindingSetDefaults.AlesisNitroDrumkit;
                        }
                        break;
                }

                return defaults.First();
            }

            return null;
        }

        private ReusableBindingSet GetBindingSetForControllerFamily(ControllerFamily family, bool menu)
        {
            var mode = menu ? GameMode.Menu : Profile.GameMode;

            if (_preferredBindsByContext.TryGetValue((mode, family), out var preferredBindingSet))
            {
                return preferredBindingSet;
            }

            var defaults = BindingsContainer.GetBindingSetsForControllerInMode(family, mode);
            if (defaults.Count > 0)
            {
                return defaults.First();
            }

            return null;
        }
        
        public void UpdateBindingsForFrame(double updateTime)
        {
            foreach (var bindings in _activeGameplayRuntimeBindings.Values)
            {
                bindings.UpdateBindingsForFrame(updateTime);
            }

            foreach (var bindings in _activeMenuRuntimeBindings.Values)
            {
                bindings.UpdateBindingsForFrame(updateTime);
            }

            _gameplayInputAggregator.UpdateForFrame(updateTime);
            _menuInputAggregator.UpdateForFrame(updateTime);
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
            foreach (var runtimeGameplayBindings in _activeGameplayRuntimeBindings.Values)
            {
                _gameplayInputAggregator.Remove(runtimeGameplayBindings);
                runtimeGameplayBindings.Dispose();
            }
            _activeGameplayRuntimeBindings.Clear();

            foreach (var runtimeMenuBindings in _activeMenuRuntimeBindings.Values)
            {
                runtimeMenuBindings.Source.Changed -= OnMenuBindingSetChanged;
                _menuInputAggregator.Remove(runtimeMenuBindings);
                runtimeMenuBindings.Dispose();
            }
            _activeMenuRuntimeBindings.Clear();

            ReleaseMicrophones();
        }

# nullable enable
        public ReusableBindingSet? GetPreferredBindingSet(GameMode mode, ControllerFamily controllerFamily)
        {
            return _preferredBindsByContext.GetValueOrDefault((mode, controllerFamily), null);
        }

        private void OnMenuBindingSetChanged(ReusableBindingSet bindingSet)
        {
            foreach (var (controller, runtimeBindings) in _activeMenuRuntimeBindings.ToList())
            {
                if (runtimeBindings.Source != bindingSet)
                {
                    continue;
                }

                SetActiveMenuBindingsForController(controller, bindingSet);
            }
        }
    }
}
