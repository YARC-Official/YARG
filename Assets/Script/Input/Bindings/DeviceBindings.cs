using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace YARG.Input
{
    public class DeviceBindings
    {
        // TODO: Differentiate menu and gameplay bindings
        private readonly Dictionary<InputControl, ControlBinding> _bindings = new();
        private readonly List<ControlBinding> _uniqueBindings = new();

        public InputDevice Device { get; }

        public DeviceBindings(InputDevice device)
        {
            Device = device;
        }

        public bool AddBinding(InputControl control, ControlBinding binding)
        {
            // Don't add the control if it's already assigned
            if (_bindings.ContainsKey(control))
                return false;

            _bindings.Add(control, binding);

            // Keep track of all unique bindings that have been added
            // Multiple controls can be assigned to the same binding
            if (!_uniqueBindings.Contains(binding))
                _uniqueBindings.Add(binding);

            return true;
        }

        public bool AddOrReplaceBinding(InputControl control, ControlBinding binding)
        {
            _ = RemoveBinding(control);
            return AddBinding(control, binding);
        }

        public bool ContainsControl(InputControl control)
        {
            return _bindings.ContainsKey(control);
        }

        public bool ContainsBinding(ControlBinding binding)
        {
            return _bindings.ContainsValue(binding);
        }

        public ControlBinding TryGetBinding(InputControl control)
        {
            return _bindings.TryGetValue(control, out var binding) ? binding : null;
        }

        public bool RemoveBinding(InputControl control)
        {
            // Get the old binding
            if (!_bindings.Remove(control, out var oldBinding))
                return false;

            // Remove from unique binds if needed
            if (!ContainsBinding(oldBinding))
                _uniqueBindings.Remove(oldBinding);

            return true;
        }

        public void ProcessInputEvent(InputEventPtr eventPtr)
        {
            foreach (var binding in _uniqueBindings)
            {
                binding.ProcessInputEvent(eventPtr);
            }
        }
    }
}