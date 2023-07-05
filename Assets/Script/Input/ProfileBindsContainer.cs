using System.Collections.Generic;
using UnityEngine.InputSystem;
using YARG.Core;

namespace YARG.Input
{
    public class ProfileBindsContainer
    {

        private YargProfile _profile;

        private Dictionary<InputDevice, DeviceBindCollection> _binds;

        public DeviceBindCollection GetBindsForDevice(InputDevice device)
        {
            if (_binds.TryGetValue(device, out var deviceBindCollection))
            {
                return deviceBindCollection;
            }

            var newBinds = new DeviceBindCollection(device);
            _binds.Add(device, newBinds);

            return newBinds;
        }

        /// <summary>
        /// Checks if this <see cref="ProfileBindsContainer"/> contains a bind for the given <see cref="InputDevice"/>.
        /// </summary>
        /// <param name="device"><see cref="InputDevice"/> to check for.</param>
        /// <returns>True if the device has at least 1 control mapped to an action.</returns>
        public bool ContainsDevice(InputDevice device)
        {
            return _binds.ContainsKey(device) && !_binds[device].IsEmpty;
        }

    }
}