using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace YARG.Input
{
    public partial class GameModeBindings : IEnumerable<ControlBinding>
    {
        private readonly List<ControlBinding> _bindings = new();

        public event GameInputProcessed InputProcessed
        {
            add
            {
                foreach (var binding in _bindings)
                {
                    binding.InputProcessed += value;
                }
            }
            remove
            {
                foreach (var binding in _bindings)
                {
                    binding.InputProcessed -= value;
                }
            }
        }

        public ControlBinding TryGetBindingByName(string name)
        {
            foreach (var binding in _bindings)
            {
                if (binding.Name == name)
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
            if (_bindings.Any((bind) => bind.Name == binding.Name || bind.Action == binding.Action))
                throw new InvalidOperationException($"A binding already exists for action {binding.Action}!");

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