using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using YARG.Input.Serialization;

namespace YARG.Input
{
    public partial class BindingCollection : IEnumerable<ControlBinding>
    {
        private readonly List<ControlBinding> _bindings = new();

        public event Action BindingsChanged
        {
            add
            {
                foreach (var binding in _bindings)
                    binding.BindingsChanged += value;
            }
            remove
            {
                foreach (var binding in _bindings)
                    binding.BindingsChanged -= value;
            }
        }

        public event GameInputProcessed InputProcessed
        {
            add
            {
                foreach (var binding in _bindings)
                    binding.InputProcessed += value;
            }
            remove
            {
                foreach (var binding in _bindings)
                    binding.InputProcessed -= value;
            }
        }

        public Dictionary<string, List<SerializedInputControl>> Serialize()
        {
            return _bindings.ToDictionary((binding) => binding.Key, (binding) => binding.Serialize());
        }

        public void Deserialize(Dictionary<string, List<SerializedInputControl>> serialized)
        {
            foreach (var (key, bindings) in serialized)
            {
                var binding = TryGetBindingByKey(key);
                if (binding is null)
                {
                    Debug.LogWarning($"Encountered invalid binding key {key}!");
                    continue;
                }

                binding.Deserialize(bindings);
            }
        }

        public void EnableInputs()
        {
            foreach (var binding in _bindings)
            {
                binding.Enable();
            }
        }

        public void DisableInputs()
        {
            foreach (var binding in _bindings)
            {
                binding.Disable();
            }
        }

        public ControlBinding TryGetBindingByKey(string key)
        {
            foreach (var binding in _bindings)
            {
                if (binding.Key == key)
                    return binding;
            }

            return null;
        }

        public ControlBinding TryGetBindingByAction(int action)
        {
            foreach (var binding in _bindings)
            {
                if (binding.Action == action)
                    return binding;
            }

            return null;
        }

        public bool ContainsControl(InputControl control)
        {
            foreach (var binding in _bindings)
            {
                if (binding.ContainsControl(control))
                    return true;
            }

            return false;
        }

        public void OnDeviceAdded(InputDevice device)
        {
            foreach (var binding in _bindings)
            {
                binding.OnDeviceAdded(device);
            }
        }

        public void OnDeviceRemoved(InputDevice device)
        {
            foreach (var binding in _bindings)
            {
                binding.OnDeviceRemoved(device);
            }
        }

        public void ProcessInputEvent(InputEventPtr eventPtr)
        {
            foreach (var binding in _bindings)
            {
                binding.ProcessInputEvent(eventPtr);
            }
        }

        // For collection initializer support
        private void Add(ControlBinding binding)
        {
            // Don't add more than one binding for the same action
            // Bindings already support multiple controls
            if (_bindings.Any((bind) => bind.Key == binding.Key || bind.Action == binding.Action))
                throw new InvalidOperationException($"A binding already exists for binding {binding.Key} with action {binding.Action}!");

            _bindings.Add(binding);
        }

        public List<ControlBinding>.Enumerator GetEnumerator()
        {
            return _bindings.GetEnumerator();
        }

        IEnumerator<ControlBinding> IEnumerable<ControlBinding>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}