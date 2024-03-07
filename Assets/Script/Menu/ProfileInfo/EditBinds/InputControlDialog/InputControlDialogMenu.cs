﻿using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using YARG.Helpers.Extensions;
using YARG.Input;
using YARG.Player;

namespace YARG.Menu.ProfileInfo
{
    // TODO: Clean this up when we get real UI
    public class InputControlDialogMenu : MonoBehaviour
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
            Axis = 0x01,
            Button = 0x02,
            // Doesn't really make sense unless we want to allow things like binding specific
            // values to a button binding or using a range of values as an axis
            // Integer = 0x04,

            // Control attributes
            Noisy = 0x0100,
            Synthetic = 0x0200,

            All = Axis | Button | Noisy | Synthetic
        }

        private const float GROUP_TIME_THRESHOLD = 0.1f;

        private State _state;
        private YargPlayer _player;
        private ControlBinding _binding;
        private AllowedControl _allowedControls = AllowedControl.All;
        private ActuationSettings _bindSettings = new();
        private InputControl _grabbedControl;

        private float? _bindGroupingTimer;
        private readonly List<InputControl> _possibleControls = new();

        private CancellationTokenSource _cancellationToken;

        [SerializeField]
        private Transform _controlContainer;
        [SerializeField]
        private GameObject _controlChooseContainer;
        [SerializeField]
        private GameObject _waitingContainer;

        [Space]
        [SerializeField]
        private GameObject _controlEntryPrefab;

        private void OnDisable()
        {
            _state = State.Done;
        }

        private void Update()
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

        public async UniTask<bool> Show(YargPlayer player, ControlBinding binding)
        {
            _state = State.Waiting;
            _player = player;
            _binding = binding;
            _grabbedControl = null;

            _bindGroupingTimer = null;
            _possibleControls.Clear();

            _cancellationToken = new();
            var token = _cancellationToken.Token;

            // Open dialog
            gameObject.SetActive(true);

            // Reset menu
            _controlContainer.DestroyChildren();
            _waitingContainer.SetActive(true);
            _controlChooseContainer.SetActive(false);

            try
            {
                // Listen until we cancel or an input is grabbed
                InputState.onChange += Listen;
                await UniTask.WaitUntil(() => _state != State.Waiting, cancellationToken: token);
                _waitingContainer.SetActive(false);
                _controlChooseContainer.SetActive(true);
                InputState.onChange -= Listen;

                // Get the actuated control
                if (_possibleControls.Count > 1)
                {
                    // Multiple controls actuated, let the user choose
                    RefreshList();

                    // Wait until the dialog is closed
                    await UniTask.WaitUntil(() => !gameObject.activeSelf, cancellationToken: token);
                }
                else if (_possibleControls.Count == 1)
                {
                    _grabbedControl = _possibleControls[0];
                }
                else
                {
                    return false;
                }

                // Add the binding
                binding.AddControl(_bindSettings, _grabbedControl);
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            finally
            {
                InputState.onChange -= Listen;

                // Close dialog
                gameObject.SetActive(false);
            }
        }

        private void RefreshList()
        {
            _controlContainer.DestroyChildren();

            foreach (var bind in _possibleControls)
            {
                var button = Instantiate(_controlEntryPrefab, _controlContainer);
                button.GetComponent<ControlEntry>().Init(bind, SelectControl);
            }
        }

        public void CancelAndClose()
        {
            _cancellationToken?.Dispose();
            gameObject.SetActive(false);
        }

        private void SelectControl(InputControl control)
        {
            _grabbedControl = control;
            gameObject.SetActive(false);
        }

        private void Listen(InputDevice device, InputEventPtr _)
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