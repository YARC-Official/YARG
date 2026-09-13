using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using YARG.Core;
using YARG.Menu.ProfileList;

namespace YARG.Input
{
    public class RuntimeBindingSet : IEnumerable<RuntimeControlBinding>
    {
        public ControllerFamily ControllerFamily { get; }
        public GameMode Mode { get; }

        private readonly List<RuntimeControlBinding> _bindings = new();

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

        public RuntimeBindingSet(GameMode mode, ControllerFamily family)
        {
            ControllerFamily = family;
            Mode = mode;
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

        public void UpdateBindingsForFrame(double updateTime)
        {
            foreach (var binding in _bindings)
            {
                binding.UpdateForFrame(updateTime);
            }
        }

        public void Add(RuntimeControlBinding binding)
        {
            // Don't add more than one binding for the same action
            // Bindings already support multiple controls
            if (_bindings.Any((bind) => bind.Key == binding.Key || bind.Action == binding.Action))
                throw new InvalidOperationException($"A binding already exists for binding {binding.Key} with action {binding.Action}!");

            _bindings.Add(binding);
        }

        public List<RuntimeControlBinding>.Enumerator GetEnumerator()
        {
            return _bindings.GetEnumerator();
        }

        IEnumerator<RuntimeControlBinding> IEnumerable<RuntimeControlBinding>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
