using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.InputSystem;
using YARG.Core.Input;
using YARG.Core.Logging;

namespace YARG.Input
{
    public delegate void GameInputProcessed(ref GameInput input);

    public abstract class RuntimeControlBinding {
        /// <summary>
        /// Fired when an input event has been processed by this binding.
        /// </summary>
        public event GameInputProcessed InputProcessed;

        /// <summary>
        /// The action enum value for this binding.
        /// </summary>
        public int Action { get; }

        /// <summary>
        /// The key string for this binding.
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// Whether or not this control is enabled.
        /// </summary>
        public bool Enabled { get; protected set; } = false;


        public RuntimeControlBinding(int action, string key)
        {
            Action = action;
            Key = key;
        }

        public void Enable()
        {
            Enabled = true;
        }

        public void Disable()
        {
            Enabled = false;
        }

        public virtual void UpdateForFrame(double updateTime) { }

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
            {
                return;
            }

            try
            {
                InputProcessed?.Invoke(ref input);
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, $"Exception when firing input event for {Key}");
            }
        }
    }

    public abstract class RuntimeControlBinding<TSingle, TSingleState> : RuntimeControlBinding
        where TSingle : RuntimeSingleBinding<TSingleState>
        where TSingleState : struct
    {
        public event Action StateChanged;

        protected List<TSingle> _bindings = new();
        public IReadOnlyList<TSingle> Bindings => _bindings;

        public RuntimeControlBinding(int action, string key) : base(action, key) { }

        public abstract bool IsControlActuated(InputControl<TSingleState> control);
        protected abstract void OnStateChanged(TSingle singleBinding, double time);

        protected void FireStateChanged()
        {
            StateChanged?.Invoke();
        }
    }
}
