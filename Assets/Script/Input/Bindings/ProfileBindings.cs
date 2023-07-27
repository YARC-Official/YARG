using System.Collections.Generic;
using System.Linq;
using UnityEngine.InputSystem;
using YARG.Core;
using YARG.Core.Game;

namespace YARG.Input
{
    public class ProfileBindings
    {
        public YargProfile Profile { get; }

        private readonly Dictionary<InputDevice, DeviceBindings> _deviceBindings;

        public event GameInputProcessed MenuInputProcessed
        {
            add
            {
                foreach (var deviceBindings in _deviceBindings.Values)
                {
                    deviceBindings.MenuInputProcessed += value;
                }
            }
            remove
            {
                foreach (var deviceBindings in _deviceBindings.Values)
                {
                    deviceBindings.MenuInputProcessed -= value;
                }
            }
        }

        public ProfileBindings(YargProfile profile)
        {
            Profile = profile;
            _deviceBindings = new();
        }

        public void EnableInputs()
        {
            foreach (var deviceBindings in _deviceBindings.Values)
            {
                deviceBindings.EnableInputs();
            }
        }

        public void DisableInputs()
        {
            foreach (var deviceBindings in _deviceBindings.Values)
            {
                deviceBindings.DisableInputs();
            }
        }

        public void SubscribeToGameplayInputs(GameMode mode, GameInputProcessed onInputProcessed)
        {
            foreach (var deviceBindings in _deviceBindings.Values)
            {
                deviceBindings[mode].Gameplay.InputProcessed += onInputProcessed;
            }
        }

        public void UnsubscribeFromGameplayInputs(GameMode mode, GameInputProcessed onInputProcessed)
        {
            foreach (var deviceBindings in _deviceBindings.Values)
            {
                deviceBindings[mode].Gameplay.InputProcessed -= onInputProcessed;
            }
        }

        public bool AddDevice(InputDevice device)
        {
            if (_deviceBindings.ContainsKey(device))
                return false;

            _deviceBindings.Add(device, new(device));
            return true;
        }

        public bool ContainsDevice(InputDevice device)
        {
            return _deviceBindings.ContainsKey(device);
        }

        public DeviceBindings TryGetBindsForDevice(InputDevice device)
        {
            return _deviceBindings.TryGetValue(device, out var bindings) ? bindings : null;
        }

        public bool RemoveDevice(InputDevice device)
        {
            return _deviceBindings.Remove(device);
        }

        // TODO: TEMPORARY
        public DeviceBindings GetBindingsForFirstDevice()
        {
            return _deviceBindings.FirstOrDefault().Value;
        }
    }
}