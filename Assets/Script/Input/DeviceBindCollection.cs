using System.Collections.Generic;
using UnityEngine.InputSystem;
using YARG.Core.Input;

namespace YARG.Input
{
    public class DeviceBindCollection
    {
        private readonly InputDevice _device;

        private readonly Dictionary<InputControl, MenuAction>   _menuBinds;
        private readonly Dictionary<InputControl, GuitarAction> _guitarBinds;
        private readonly Dictionary<InputControl, DrumAction>   _drumBinds;

        public bool IsEmpty => _menuBinds.Count == 0 && _guitarBinds.Count == 0 && _drumBinds.Count == 0;

        public DeviceBindCollection(InputDevice device) : this()
        {
            _device = device;
        }

        public DeviceBindCollection()
        {
            _menuBinds = new Dictionary<InputControl, MenuAction>();
            _guitarBinds = new Dictionary<InputControl, GuitarAction>();
            _drumBinds = new Dictionary<InputControl, DrumAction>();
        }

        public bool ContainsControl(InputControl control)
        {
            return _menuBinds.ContainsKey(control) || _guitarBinds.ContainsKey(control) || _drumBinds.ContainsKey(control);
        }
    }
}