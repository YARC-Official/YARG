using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Localization;
using YARG.Core.Input;
using YARG.Input.Serialization;

namespace YARG.Input
{
    public delegate void GameInputProcessed(ref GameInput input);

    public class ActuationSettings
    {
        public float ButtonPressThreshold = 0.5f;
        public float AxisDeltaThreshold = 0.05f;
        public int IntegerDeltaThreshold = 1;
    }

    /// <summary>
    /// A binding to one or more controls.
    /// </summary>
    public abstract class ControlBinding
    {
        /// <summary>
        /// Fired when a binding has been added or removed.
        /// </summary>
        public event Action BindingsChanged;

        /// <summary>
        /// Fired when an input event has been processed by this binding.
        /// </summary>
        public event GameInputProcessed InputProcessed;

        /// <summary>
        /// The name for this binding.
        /// </summary>
        public LocalizedString Name { get; }

        /// <summary>
        /// The key string for this binding.
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// The action enum value for this binding.
        /// </summary>
        public int Action { get; }

        /// <summary>
        /// Whether or not this control is enabled.
        /// </summary>
        public bool Enabled { get; protected set; } = false;

        public ControlBinding(string name, int action)
        {
            Key = name;
            Name = new("Bindings", name);
            Action = action;
        }

        public abstract List<SerializedInputControl> Serialize();
        public abstract void Deserialize(List<SerializedInputControl> serialized);

        public abstract bool IsControlCompatible(InputControl control);
        public abstract bool IsControlActuated(ActuationSettings settings, InputControl control, InputEventPtr eventPtr);

        public abstract bool AddControl(ActuationSettings settings, InputControl control);
        public abstract bool RemoveControl(InputControl control);
        public abstract bool ContainsControl(InputControl control);

        public virtual void Enable()
        {
            Enabled = true;
        }

        public virtual void Disable()
        {
            Enabled = false;
        }

        public virtual void UpdateForFrame() { }
        public abstract void ProcessInputEvent(InputEventPtr eventPtr);

        public abstract void OnDeviceAdded(InputDevice device);
        public abstract void OnDeviceRemoved(InputDevice device);

        protected void FireBindingsChanged()
        {
            BindingsChanged?.Invoke();
        }

        protected void FireInputEvent(double time, int value)
        {
            var input = new GameInput(time, Action, value);
            FireInputEvent(ref input);
        }

        protected void FireInputEvent(double time, float value)
        {
            var input = new GameInput(time, Action, value);
            FireInputEvent(ref input);
        }

        protected void FireInputEvent(double time, bool value)
        {
            var input = new GameInput(time, Action, value);
            FireInputEvent(ref input);
        }

        protected void FireInputEvent(ref GameInput input)
        {
            if (!Enabled)
                return;

            try
            {
                InputProcessed?.Invoke(ref input);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Exception when firing input event for {Key}!");
                Debug.LogException(ex);
            }
        }
    }

    /// <summary>
    /// A binding to one or more controls.
    /// </summary>
    public abstract class ControlBinding<TState, TParams> : ControlBinding
        where TState : struct
        where TParams : new()
    {
        public class SingleBinding
        {
            public InputControl<TState> Control { get; }
            public TState State { get; private set; }
            public TParams Parameters = new();

            public InputControl InputControl => Control;

            public SingleBinding(InputControl<TState> control)
            {
                Control = control;
            }

            public TState UpdateState(InputEventPtr eventPtr)
            {
                if (Control.HasValueChangeInEvent(eventPtr))
                {
                    State = Control.ReadValueFromEvent(eventPtr);
                }

                return State;
            }
        }

        private List<SerializedInputControl> _unresolvedBindings = new();

        protected List<SingleBinding> _bindings = new();
        public IReadOnlyList<SingleBinding> Bindings => _bindings;

        public ControlBinding(string name, int action) : base(name, action)
        {
        }

        public override List<SerializedInputControl> Serialize()
        {
            return new(_bindings.Select((binding) => SerializeControl(binding))
                .Concat(_unresolvedBindings));
        }

        public override void Deserialize(List<SerializedInputControl> serialized)
        {
            if (serialized is null)
            {
                Debug.LogWarning($"Encountered invalid controls list for binding {Key}!");
                return;
            }

            foreach (var binding in serialized)
            {
                if (binding is null || string.IsNullOrEmpty(binding.ControlPath) || binding.Device is null ||
                    string.IsNullOrEmpty(binding.Device.Layout) || string.IsNullOrEmpty(binding.Device.Hash))
                {
                    Debug.LogWarning($"Encountered invalid control for binding {Key}!");
                    return;
                }

                // Don't bail out on invalid parameters, they're not necessary for deserialization
                binding.Parameters ??= new();

                // Bindings will be resolved later
                _unresolvedBindings.Add(binding);
            }
        }

        public override bool IsControlCompatible(InputControl control)
        {
            return IsControlCompatible(control, out _);
        }

        public virtual bool IsControlCompatible(InputControl control, out InputControl<TState> typedControl)
        {
            if (control is InputControl<TState> tControl)
            {
                typedControl = tControl;
                return true;
            }

            typedControl = null;
            return false;
        }

        public override bool IsControlActuated(ActuationSettings settings, InputControl control, InputEventPtr eventPtr)
        {
            return IsControlCompatible(control, out var tControl) && IsControlActuated(settings, tControl, eventPtr);
        }

        public abstract bool IsControlActuated(ActuationSettings settings, InputControl<TState> control, InputEventPtr eventPtr);

        public override bool AddControl(ActuationSettings settings, InputControl control)
        {
            return IsControlCompatible(control, out var tControl) && AddControl(settings, tControl);
        }

        public override bool RemoveControl(InputControl control)
        {
            return IsControlCompatible(control, out var tControl) && RemoveControl(tControl);
        }

        public override bool ContainsControl(InputControl control)
        {
            return IsControlCompatible(control, out var tControl) && ContainsControl(tControl);
        }

        public bool AddControl(ActuationSettings settings, InputControl<TState> control)
        {
            if (ContainsControl(control))
                return false;

            var binding = new SingleBinding(control);
            _bindings.Add(binding);
            OnControlAdded(settings, binding);
            return true;
        }

        public bool RemoveControl(InputControl<TState> control)
        {
            if (!TryGetBinding(control, out var binding))
                return false;

            bool removed = _bindings.Remove(binding);
            if (removed)
                OnControlRemoved(binding);

            return removed;
        }

        public bool ContainsControl(InputControl<TState> control)
        {
            return TryGetBinding(control, out _);
        }

        public bool AddBinding(ActuationSettings settings, SingleBinding binding)
        {
            if (ContainsBinding(binding))
                return false;

            _bindings.Add(binding);
            OnControlAdded(settings, binding);
            return true;
        }

        public bool RemoveBinding(SingleBinding binding)
        {
            bool removed = _bindings.Remove(binding);
            if (removed)
                OnControlRemoved(binding);

            return removed;
        }

        public bool ContainsBinding(SingleBinding binding)
        {
            return _bindings.Contains(binding);
        }

        public bool TryGetBinding(InputControl<TState> control, out SingleBinding foundBinding)
        {
            foreach (var binding in _bindings)
            {
                if (binding.Control == control)
                {
                    foundBinding = binding;
                    return true;
                }
            }

            foundBinding = null;
            return false;
        }

        public override void OnDeviceAdded(InputDevice device)
        {
            // Search by index, can't modify a collection while enumerating it
            bool controlsModified = false;
            for (int i = 0; i < _unresolvedBindings.Count; i++)
            {
                var binding = _unresolvedBindings[i];
                if (!binding.Device.MatchesDevice(device))
                    continue;

                var deserialized = DeserializeControl(device, binding);
                if (deserialized is null)
                    continue;

                _unresolvedBindings.RemoveAt(i);
                i--;
                _bindings.Add(deserialized);
                controlsModified = true;
            }

            if (controlsModified)
                FireBindingsChanged();
        }

        public override void OnDeviceRemoved(InputDevice device)
        {
            // Search by index, can't modify a collection while enumerating it
            bool controlsModified = false;
            for (int i = 0; i < _bindings.Count; i++)
            {
                var binding = _bindings[i];
                if (binding.InputControl.device != device)
                    continue;

                var serialized = SerializeControl(binding);
                if (serialized is null)
                    continue;

                _bindings.RemoveAt(i);
                i--;
                _unresolvedBindings.Add(serialized);
                controlsModified = true;
            }

            if (controlsModified)
                FireBindingsChanged();
        }

        protected virtual void OnControlAdded(ActuationSettings settings, SingleBinding binding)
        {
            FireBindingsChanged();
        }

        protected virtual void OnControlRemoved(SingleBinding binding)
        {
            FireBindingsChanged();
        }

        protected virtual SerializedInputControl SerializeControl(SingleBinding binding)
        {
            return new SerializedInputControl()
            {
                Device = binding.Control.device.Serialize(),
                ControlPath = binding.Control.path,
            };
        }

        protected virtual SingleBinding DeserializeControl(InputDevice device, SerializedInputControl serialized)
        {
            var control = InputControlPath.TryFindControl(device, serialized.ControlPath);
            if (control == null)
            {
                Debug.LogWarning($"Could not find control {serialized.ControlPath} on device {device}!");
                return null;
            }

            if (control is not InputControl<TState> tControl)
            {
                Debug.LogWarning($"Found control {serialized.ControlPath}, but it was not of the right type! Expected a derivative of {typeof(InputControl<TState>)}, found {control.GetType()}");
                return null;
            }

            return new SingleBinding(tControl);
        }
    }
}