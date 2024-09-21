using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using YARG.Core.Input;
using YARG.Core.Logging;
using YARG.Input.Serialization;

namespace YARG.Input
{
    public delegate void GameInputProcessed(ref GameInput input);

    public class ActuationSettings
    {
        public static readonly ActuationSettings Default = new();

        public float ButtonPressThreshold  = 0.5f;
        public float AxisDeltaThreshold    = 0.05f;
        public int   IntegerDeltaThreshold = 1;
    }

    /// <summary>
    /// A binding to one or more controls.
    /// </summary>
    public abstract class ControlBinding
    {
        public static event Action<ControlBinding, InputControl> BindingAdded;
        public static event Action<ControlBinding, InputControl> BindingRemoved;

        /// <summary>
        /// Fired when a binding has been added or removed.
        /// </summary>
        public event Action BindingsChanged;

        /// <summary>
        /// Fired when an input event has been processed by this binding.
        /// </summary>
        public event GameInputProcessed InputProcessed;

        /// <summary>
        /// The unlocalized name for this binding.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// The alternate unlocalized name for this binding, representing lefty-flip.
        /// </summary>
        public string NameLefty { get; }

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

        protected double _lastEventTime;

        public ControlBinding(string name, int action)
        {
            Key = name;

            Name = name;
            NameLefty = Name;

            Action = action;
        }

        public ControlBinding(string name, string nameLefty, int action)
        {
            Key = name;

            Name = name;
            NameLefty = nameLefty;

            Action = action;
        }

#nullable enable
        public abstract SerializedControlBinding? Serialize();
        public abstract void Deserialize(SerializedControlBinding serialized);

#nullable disable

        public abstract bool IsControlCompatible(InputControl control);
        public abstract bool IsControlActuated(ActuationSettings settings, InputControl control);

        public bool AddControl(InputControl control) => AddControl(ActuationSettings.Default, control);
        public abstract bool AddControl(ActuationSettings settings, InputControl control);
        public abstract bool RemoveControl(InputControl control);
        public abstract bool ContainsControl(InputControl control);

        public abstract bool ContainsBindingsForDevice(InputDevice device);
        public abstract void ClearBindingsForDevice(InputDevice device);
        public abstract void ClearAllBindings();

        public virtual void Enable()
        {
            Enabled = true;
        }

        public virtual void Disable()
        {
            Enabled = false;
        }

        public virtual void UpdateForFrame(double updateTime)
        {
        }

        public abstract void OnDeviceAdded(InputDevice device);
        public abstract void OnDeviceRemoved(InputDevice device);

        protected void FireBindingAdded(InputControl control)
        {
            BindingAdded?.Invoke(this, control);
        }

        protected void FireBindingRemoved(InputControl control)
        {
            BindingRemoved?.Invoke(this, control);
        }

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

        protected virtual void FireInputEvent(double time, bool value)
        {
            var input = new GameInput(time, Action, value);
            FireInputEvent(ref input);
        }

        protected void FireInputEvent(ref GameInput input)
        {
            if (!Enabled) return;

            try
            {
                _lastEventTime = input.Time;
                InputProcessed?.Invoke(ref input);
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, $"Exception when firing input event for {Key}");
            }
        }
    }

    public abstract class SingleBinding<TState>
        where TState : struct
    {
        public InputControl<TState> Control { get; }
        public TState               State   { get; protected set; }

        public event Action<TState> StateChanged;

        public SingleBinding(InputControl<TState> control)
        {
            Control = control;
        }

        public SingleBinding(InputControl<TState> control, SerializedInputControl serialized)
            : this(control)
        {
        }

        public virtual void UpdateState(double time)
        {
            State = Control.value;
            InvokeStateChanged(State);
        }

        protected void InvokeStateChanged(TState state)
        {
            StateChanged?.Invoke(state);
        }

        public virtual SerializedInputControl Serialize()
        {
            // InputControl.path uses the device name,
            // which is not guaranteed to be stable across different runs of the game
            // (e.g. XInputGuitarHeroGuitar1 in one session could be just XInputGuitarHeroGuitar in another)
            // Swap that out for the device layout instead, indicated by <angle brackets>
            string path = Control.path.Replace(Control.device.name, $"<{Control.device.layout}>");
            return new(Control.device.Serialize(), path);
        }
    }

    /// <summary>
    /// A binding to one or more controls.
    /// </summary>
    public abstract class ControlBinding<TState, TBinding> : ControlBinding, IInputStateChangeMonitor
        where TState : struct
        where TBinding : SingleBinding<TState>
    {
        public event Action StateChanged;

        private List<SerializedInputControl> _unresolvedBindings = new();

        protected List<TBinding>          _bindings = new();
        public    IReadOnlyList<TBinding> Bindings => _bindings;

        public ControlBinding(string name, int action) : base(name, action)
        {
        }

        public ControlBinding(string name, string nameLefty, int action) : base(name, nameLefty, action)
        {
        }

#nullable enable
        public override SerializedControlBinding? Serialize()
        {
            var serialized = new SerializedControlBinding()
            {
                Parameters = SerializeParameters()
            };

            foreach (var binding in _bindings)
            {
                var serializedBind = SerializeControl(binding);
                if (serializedBind is null) continue;

                serialized.Controls.Add(serializedBind);
            }

            serialized.Controls.AddRange(_unresolvedBindings);

            if (serialized.Controls.Count < 1) return null;

            return serialized;
        }

        public override void Deserialize(SerializedControlBinding? serialized)
        {
            if (serialized is null || serialized.Controls is null) return;

            DeserializeParameters(serialized.Parameters);

            foreach (var binding in serialized.Controls)
            {
                if (binding is null || string.IsNullOrEmpty(binding.ControlPath) || binding.Device is null ||
                    string.IsNullOrEmpty(binding.Device.Layout) || string.IsNullOrEmpty(binding.Device.Hash))
                {
                    YargLogger.LogFormatWarning("Encountered invalid control for binding {0}!", Key);
                    return;
                }

                // Don't bail out on invalid parameters, they're not necessary for deserialization
                binding.Parameters ??= new();

                // Bindings will be resolved later
                _unresolvedBindings.Add(binding);
            }
        }

        protected virtual Dictionary<string, string> SerializeParameters() => new();
        protected virtual void DeserializeParameters(Dictionary<string, string> parameters)
        {
        }
#nullable disable

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

        public override bool IsControlActuated(ActuationSettings settings, InputControl control)
        {
            return IsControlCompatible(control, out var tControl) && IsControlActuated(settings, tControl);
        }

        public abstract bool IsControlActuated(ActuationSettings settings, InputControl<TState> control);

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
            if (ContainsControl(control)) return false;

            var binding = CreateBinding(settings, control);
            AddBinding(binding);
            FireBindingsChanged();
            return true;
        }

        public bool RemoveControl(InputControl<TState> control)
        {
            if (!TryGetBinding(control, out var binding)) return false;

            return RemoveBinding(binding);
        }

        public bool ContainsControl(InputControl<TState> control)
        {
            return TryGetBinding(control, out _);
        }

        public bool RemoveBinding(TBinding binding)
        {
            return RemoveBindings((b) => b == binding);
        }

        public bool ContainsBinding(TBinding binding)
        {
            return _bindings.Contains(binding);
        }

        public bool TryGetBinding(InputControl<TState> control, out TBinding foundBinding)
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

        public override bool ContainsBindingsForDevice(InputDevice device)
        {
            foreach (var binding in _bindings)
            {
                if (binding.Control.device == device)
                    return true;
            }

            foreach (var serialized in _unresolvedBindings)
            {
                if (serialized.Device.MatchesDevice(device))
                    return true;
            }

            return false;
        }

        public override void ClearBindingsForDevice(InputDevice device)
        {
            RemoveBindings((binding) => binding.Control.device == device);

            for (int i = 0; i < _unresolvedBindings.Count; i++)
            {
                var binding = _unresolvedBindings[i];
                if (binding.Device.MatchesDevice(device))
                {
                    _unresolvedBindings.RemoveAt(i);
                    i--;
                }
            }
        }

        public override void ClearAllBindings()
        {
            for (int i = 0; i < _bindings.Count; i++)
            {
                var binding = _bindings[i];
                InputState.RemoveChangeMonitor(binding.Control, this, i);
            }

            _bindings.Clear();
            _unresolvedBindings.Clear();
        }

        private void AddBinding(TBinding binding)
        {
            _bindings.Add(binding);
            InputState.AddChangeMonitor(binding.Control, this, _bindings.Count - 1);
            FireBindingAdded(binding.Control);
        }

        private bool RemoveBindings(Func<TBinding, bool> selector)
        {
            bool removed = false;
            int monitorIndex = 0;
            for (int i = 0; i < _bindings.Count; i++, monitorIndex++)
            {
                var binding = _bindings[i];

                // Always remove change monitor, as we need to update the monitor indexes
                InputState.RemoveChangeMonitor(binding.Control, this, monitorIndex);

                if (selector(binding))
                {
                    removed = true;
                    _bindings.RemoveAt(i);
                    FireBindingRemoved(binding.Control);
                    i--;
                }
                else
                {
                    // Re-add change monitor with the updated list index
                    InputState.AddChangeMonitor(binding.Control, this, i);
                }
            }

            if (removed) FireBindingsChanged();

            return removed;
        }

#nullable enable
        public override void OnDeviceAdded(InputDevice device)
        {
            // Search by index, can't modify a collection while enumerating it
            bool controlsModified = false;
            for (int i = 0; i < _unresolvedBindings.Count; i++)
            {
                var binding = _unresolvedBindings[i];
                if (!binding.Device.MatchesDevice(device)) continue;

                // Remove regardless of if deserialization fails, no point keeping broken bindings around
                _unresolvedBindings.RemoveAt(i);
                i--;

                var deserialized = DeserializeControl(device, binding);
                if (deserialized is null) continue;

                AddBinding(deserialized);
                controlsModified = true;
            }

            if (controlsModified) FireBindingsChanged();
        }

        public override void OnDeviceRemoved(InputDevice device)
        {
            RemoveBindings((binding) =>
            {
                if (binding.Control.device != device) return false;

                var serialized = SerializeControl(binding);
                if (serialized is null) return false;

                _unresolvedBindings.Add(serialized);
                return true;
            });
        }
#nullable disable

        protected abstract TBinding CreateBinding(ActuationSettings settings, InputControl<TState> binding);

        void IInputStateChangeMonitor.NotifyControlStateChanged(InputControl control, double time,
            InputEventPtr eventPtr,
            long monitorIndex)
        {
            if (!eventPtr.valid)
            {
                YargLogger.LogFormatError("Invalid eventPtr received for control {0}!", control);
                return;
            }

            if (monitorIndex >= _bindings.Count)
            {
                YargLogger.LogFormatError("Invalid state monitor index {0}!", monitorIndex);
                return;
            }

            var binding = _bindings[(int) monitorIndex];
            if (binding.Control != control)
            {
                YargLogger.LogFormatError("State monitor index {0} does not match binding! Expected {1}, got {2}",
                    monitorIndex, binding.Control, control);
                return;
            }

            binding.UpdateState(eventPtr.time);
            OnStateChanged(binding, eventPtr.time);
            FireStateChanged();
        }

        void IInputStateChangeMonitor.NotifyTimerExpired(InputControl control, double time, long monitorIndex,
            int timerIndex)
        {
        }

        protected abstract void OnStateChanged(TBinding binding, double time);

        protected void FireStateChanged()
        {
            StateChanged?.Invoke();
        }

#nullable enable
        protected virtual SerializedInputControl? SerializeControl(TBinding binding)
        {
            return binding.Serialize();
        }

        private TBinding? DeserializeControl(InputDevice device, SerializedInputControl serialized)
        {
            var control = InputControlPath.TryFindControl(device, serialized.ControlPath);
            if (control == null)
            {
                // Fallback for older bindings which incorrectly serialized using device.name instead of device.layout
                var elements = InputControlPath.Parse(serialized.ControlPath).ToArray();
                if (elements.Length > 0)
                {
                    string name = elements[0].name;
                    if (!string.IsNullOrEmpty(name))
                    {
                        string newPath = serialized.ControlPath.Replace(name, $"<{device.layout}>");
                        control = InputControlPath.TryFindControl(device, newPath);
                    }
                }

                if (control == null)
                {
                    YargLogger.LogFormatWarning("Could not find control {0} on device {1}!", serialized.ControlPath,
                        device);
                    return null;
                }
            }

            if (control is not InputControl<TState> tControl)
            {
                YargLogger.LogFormatWarning(
                    "Found control {0}, but it was not of the right type! Expected a derivative of {1}, found {2}",
                    serialized.ControlPath, typeof(InputControl<TState>), control.GetType());
                return null;
            }

            return DeserializeControl(tControl, serialized);
        }

        protected abstract TBinding DeserializeControl(InputControl<TState> control, SerializedInputControl serialized);
    }
}