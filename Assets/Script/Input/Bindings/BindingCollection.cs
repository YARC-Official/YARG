using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XInput;
using YARG.Core;
using YARG.Core.Extensions;
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

        public GameMode? Mode { get; }

        public bool IsMenu => Mode == null;
        public bool IsGameplay => Mode != null;

        public BindingCollection(GameMode? mode)
        {
            Mode = mode;
        }

#nullable enable
        public SerializedBindingCollection? Serialize()
        {
            var serialized = new SerializedBindingCollection();
            foreach (var binding in _bindings)
            {
                var serializedBind = binding.Serialize();
                if (serializedBind is null)
                    continue;

                serialized.Bindings.Add(binding.Key, serializedBind);
            }

            if (serialized.Bindings.Count < 1)
                return null;

            return serialized;
        }

        public void Deserialize(SerializedBindingCollection? serialized)
        {
            if (serialized is null || serialized.Bindings is null)
                return;

            foreach (var (key, bindings) in serialized.Bindings)
            {
                var binding = TryGetBindingByKey(key);
                if (binding is null)
                    continue;

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

        public ControlBinding? TryGetBindingByKey(string key)
        {
            foreach (var binding in _bindings)
            {
                if (binding.Key == key)
                    return binding;
            }

            return null;
        }

        public ControlBinding? TryGetBindingByAction<TAction>(TAction action)
            where TAction : unmanaged, Enum
        {
            return TryGetBindingByAction(action.Convert());
        }

        public ControlBinding? TryGetBindingByAction(int action)
        {
            foreach (var binding in _bindings)
            {
                if (binding.Action == action)
                    return binding;
            }

            return null;
        }
#nullable disable

        public bool ContainsControl(InputControl control)
        {
            foreach (var binding in _bindings)
            {
                if (binding.ContainsControl(control))
                    return true;
            }

            return false;
        }

        public bool ContainsBindingsForDevice(InputDevice device)
        {
            foreach (var binding in _bindings)
            {
                if (binding.ContainsBindingsForDevice(device))
                    return true;
            }

            return false;
        }

        public void ClearBindingsForDevice(InputDevice device)
        {
            foreach (var binding in _bindings)
            {
                binding.ClearBindingsForDevice(device);
            }
        }

        public void ClearAllBindings()
        {
            foreach (var binding in _bindings)
            {
                binding.ClearAllBindings();
            }
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

        public void UpdateBindingsForFrame(double updateTime)
        {
            foreach (var binding in _bindings)
            {
                binding.UpdateForFrame(updateTime);
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