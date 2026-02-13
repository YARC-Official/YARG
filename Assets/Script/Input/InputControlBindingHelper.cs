using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using YARG.Player;

namespace YARG.Input
{
    /// <summary>
    /// Contains the bits that capture input keypresses when creating control bindings
    /// </summary>
    public class InputControlBindingHelper : MonoSingleton<InputControlBindingHelper>
    {
        private enum State
        {
            Waiting,
            Select,
            Done
        }

        [Flags]
        private enum AllowedControl
        {
            None = 0,

            // Control types
            Axis   = 0x01,
            Button = 0x02,
            // Doesn't really make sense unless we want to allow things like binding specific
            // values to a button binding or using a range of values as an axis
            // Integer = 0x04,

            // Control attributes
            Noisy     = 0x0100,
            Synthetic = 0x0200,

            All = Axis | Button | Noisy | Synthetic
        }

        private const float GROUP_TIME_THRESHOLD = 0.1f;

        private State             _state;
        private YargPlayer        _player;
        private ControlBinding    _binding;
        private AllowedControl    _allowedControls = AllowedControl.All;
        private ActuationSettings _bindSettings    = new();
        private InputControl      _grabbedControl;

        private          float?             _bindGroupingTimer;
        private readonly List<InputControl> _possibleControls = new();

        private CancellationTokenSource _cancellationToken;

        /// <summary>
        /// Eventually returns one or more controls that the user actuated. Caller must add the binding if desired.
        /// </summary>
        /// <param name="player"></param>
        /// <param name="token"></param>
        /// <param name="binding"></param>
        /// <returns></returns>
        public async UniTask<List<InputControl>> GetControl(YargPlayer player, CancellationToken token, ControlBinding binding)
        {
            _state = State.Waiting;
            _possibleControls.Clear();
            _binding = binding;
            _player = player;

            try
            {
                // Listen until we cancel or an input is grabbed
                InputState.onChange += Listen;
                await UniTask.WaitUntil(() => _state != State.Waiting, cancellationToken: token);
                InputState.onChange -= Listen;

                return _possibleControls;
            }
            catch (OperationCanceledException)
            {
                _state = State.Waiting;
                return _possibleControls;
            }
            finally
            {
                _state = State.Done;
                _bindGroupingTimer = null;
                InputState.onChange -= Listen;
            }
        }

        public void Update()
        {
            // The grouping timer has not started yet
            if (_bindGroupingTimer is null) return;

            if (_bindGroupingTimer <= 0f)
            {
                _state = State.Select;
                _bindGroupingTimer = null;
            }
            else
            {
                _bindGroupingTimer -= Time.deltaTime;
            }
        }

        public void Listen(InputDevice device, InputEventPtr iep)
        {
            // Ignore controls for devices not added to the player's bindings
            if (!_player.Bindings.ContainsDevice(device))
                return;

            // The eventPtr is not used here, as it is not guaranteed to be valid,
            // and even if it were, it would no longer be useful for determining which controls changed
            // since the state from that event has already been written to the device buffers by this time
            foreach (var control in device.allControls)
            {
                // Ignore disallowed and inactive controls
                if (!ControlAllowed(control) || !_binding.IsControlActuated(_bindSettings, control))
                    continue;

                if (!_possibleControls.Contains(control))
                    _possibleControls.Add(control);

                // Reset timer
                _bindGroupingTimer = GROUP_TIME_THRESHOLD;
            }
        }

        private bool ControlAllowed(InputControl control)
        {
            // AnyKeyControl is excluded as it would always be active
            if (control is AnyKeyControl)
            {
                return false;
            }

            // Check that the control is allowed
            if ((control.noisy && (_allowedControls & AllowedControl.Noisy) == 0) ||
                (control.synthetic && (_allowedControls & AllowedControl.Synthetic) == 0) ||
                // Buttons must be checked before axes, as ButtonControl inherits from AxisControl
                (control is ButtonControl && (_allowedControls & AllowedControl.Button) == 0) ||
                (control is AxisControl && (_allowedControls & AllowedControl.Axis) == 0))
            {
                return false;
            }

            // Modifier keys on keyboard have both individual left/right controls and combined controls,
            // we want to ignore the combined controls to prevent ambiguity
            if (control.device is Keyboard keyboard &&
                (control == keyboard.shiftKey ||
                control == keyboard.ctrlKey ||
                control == keyboard.altKey))
            {
                return false;
            }

            return true;
        }
    }
}