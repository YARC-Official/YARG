using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.XR;
using YARG.Core;
using YARG.Helpers;
using YARG.Input.Bindings;
using YARG.Menu.ProfileList;
using static UnityEditor.AddressableAssets.Build.Layout.BuildLayout;

namespace YARG.Input.Bindings
{
    public class RuntimeBindingSet : IEnumerable<RuntimeControlBinding>, IDisposable
    {
        public ReusableBindingSet Source { get; }
        public InputDevice Controller { get; }
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

        public RuntimeBindingSet(InputDevice controller, GameMode mode)
        {
            Controller = controller;
            Mode = mode;
        }

        public RuntimeBindingSet(InputDevice controller, ReusableBindingSet reusableBindings) : this(controller, reusableBindings.Mode)
        {
            Source = reusableBindings;

            foreach (var binding in reusableBindings.Bindings.Values)
            {
                RuntimeControlBinding newBind = binding switch
                {
                    ReusableButtonBinding button => button.Subtype switch {
                        ReusableButtonBindingSubtype.Impulse => new RuntimeImpulseBinding(controller, button),
                        ReusableButtonBindingSubtype.Drum => new RuntimeDrumPadBinding(controller, button),
                        _ => new RuntimeButtonBinding(controller, button),
                    },
                    ReusableAxisBinding axis => new RuntimeAxisBinding(controller, axis),
                    ReusableIntegerBinding integer => throw new NotImplementedException(), // TODO-FRICK
                    _ => throw new ArgumentOutOfRangeException("Unrecognized reusable binding type")
                };

                Add(newBind);

                switch (newBind)
                {
                    case RuntimeButtonBinding button:
                        for (var i = 0; i < button.Bindings.Count; i++)
                        {
                            InputState.AddChangeMonitor(button.Bindings[i].Control, button, i);
                        }
                        break;
                    case RuntimeAxisBinding axis:
                        for (var i = 0; i < axis.Bindings.Count; i++)
                        {
                            InputState.AddChangeMonitor(axis.Bindings[i].Control, axis, i);
                        }
                        break;
                    case RuntimeIntegerBinding integer:
                        for (var i = 0; i < integer.Bindings.Count; i++)
                        {
                            InputState.AddChangeMonitor(integer.Bindings[i].Control, integer, i);
                        }
                        break;
                    default:
                        throw new NotImplementedException();
                }
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

        public void Dispose()
        {
            DisableInputs();

            foreach (var binding in _bindings)
            {
                binding.Dispose();
            }

            _bindings.Clear();
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
