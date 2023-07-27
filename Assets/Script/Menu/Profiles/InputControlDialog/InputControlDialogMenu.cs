using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;
using YARG.Helpers.Extensions;
using YARG.Input;

namespace YARG.Menu.Profiles
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

        private static InputControlDialogMenu _instance;

        private static State _state;
        private static InputDevice _inputDevice;
        private static ControlBinding _binding;
        private static AllowedControl _allowedControls = AllowedControl.All;
        private static ActuationSettings _bindSettings = new();
        private static InputControl _grabbedControl;

        private static float? _bindGroupingTimer;
        private static readonly List<InputControl> _possibleControls = new();

        private static CancellationTokenSource _cancellationToken;

        [SerializeField]
        private Transform _controlContainer;
        [SerializeField]
        private GameObject _waitingText;

        [Space]
        [SerializeField]
        private GameObject _controlEntryPrefab;

        private void Awake()
        {
            _instance = this;
        }

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

        public static async UniTask<bool> Show(InputDevice device, ControlBinding binding)
        {
            _state = State.Waiting;
            _inputDevice = device;
            _binding = binding;
            _grabbedControl = null;

            _bindGroupingTimer = null;
            _possibleControls.Clear();

            _cancellationToken = new();
            var token = _cancellationToken.Token;

            // Open dialog
            MenuNavigator.Instance.PushMenu(MenuNavigator.Menu.InputControlDialog);

            // Reset menu
            _instance._controlContainer.DestroyChildren();
            _instance._waitingText.SetActive(true);

            try
            {
                // Create a listener
                var listener = InputSystem.onEvent.ForDevice(_inputDevice).Call(Listen);

                // Listen until we cancel or an input is grabbed
                await UniTask.WaitUntil(() => _state != State.Waiting, cancellationToken: token);
                _instance._waitingText.SetActive(false);

                // Dispose
                listener?.Dispose();

                // Get the actuated control
                if (_possibleControls.Count > 1)
                {
                    // Multiple controls actuated, let the user choose
                    _instance.RefreshList();

                    // Wait until the dialog is closed
                    await UniTask.WaitUntil(() => !_instance.gameObject.activeSelf, cancellationToken: token);
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
                MenuNavigator.Instance.PopMenu();
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
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
            try
            {
                _cancellationToken?.Dispose();
            }
            finally
            {
                MenuNavigator.Instance.PopMenu();
            }
        }

        private static void SelectControl(InputControl control)
        {
            _grabbedControl = control;
        }

        private static void Listen(InputEventPtr eventPtr)
        {
            // Only take state events
            if (!eventPtr.IsA<StateEvent>() && !eventPtr.IsA<DeltaStateEvent>())
                return;

            var flags = InputManager.DEFAULT_CONTROL_ENUMERATION_FLAGS;
            var activeControls = eventPtr.EnumerateControls(flags, _inputDevice)
                .Where((control) => ControlAllowed(control) &&
                    _binding.IsControlActuated(_bindSettings, control, eventPtr));

            // Add all controls
            foreach (var control in activeControls)
            {
                if (!_possibleControls.Contains(control))
                {
                    _possibleControls.Add(control);
                }

                // Reset timer
                _bindGroupingTimer = GROUP_TIME_THRESHOLD;
            }
        }

        private static bool ControlAllowed(InputControl control)
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

            return true;
        }
    }
}