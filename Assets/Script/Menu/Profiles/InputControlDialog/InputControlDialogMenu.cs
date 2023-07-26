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
using YARG.Player.Input;

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

        private const float GROUP_TIME_THRESHOLD = 0.1f;

        private static InputControlDialogMenu _instance;

        private static State _state;
        private static InputDevice _inputDevice;
        private static InputControl _grabbedControl;

        private static float? _bindGroupingTimer;
        private static readonly List<InputControl<float>> _possibleControls = new();

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

        private void RefreshList()
        {
            _controlContainer.DestroyChildren();

            foreach (var bind in _possibleControls)
            {
                var button = Instantiate(_controlEntryPrefab, _controlContainer);
                button.GetComponent<ControlEntry>().Init(bind, SelectAndClose);
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

        private static void SelectAndClose(InputControl control)
        {
            _grabbedControl = control;
            MenuNavigator.Instance.PopMenu();
        }

        private static void Listen(InputEventPtr eventPtr)
        {
            // Only take state events
            if (!eventPtr.IsA<StateEvent>() && !eventPtr.IsA<DeltaStateEvent>())
            {
                return;
            }

            // Find all active float-returning controls
            // Only controls that have changed
            //  | Constantly-changing controls like accelerometers
            //  | Non-physical controls like stick up/down/left/right
            var flags = InputControlExtensions.Enumerate.IgnoreControlsInCurrentState
                | InputControlExtensions.Enumerate.IncludeNoisyControls
                | InputControlExtensions.Enumerate.IncludeSyntheticControls;
            var activeControls = from control in eventPtr.EnumerateControls(flags, _inputDevice)
                where ControlAllowedAndActive(control, eventPtr)
                select control as InputControl<float>;

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

        private static bool ControlAllowedAndActive(InputControl control, InputEventPtr eventPtr)
        {
            // AnyKeyControl is excluded as it would always be active
            if (control is not InputControl<float> floatControl || floatControl is AnyKeyControl)
            {
                return false;
            }

            // Ensure control is pressed
            if (!InputStrategy.IsControlPressed(floatControl, eventPtr))
            {
                return false;
            }

            return true;
        }

        public static async UniTask<InputControl> Show(InputDevice device)
        {
            _inputDevice = device;
            _grabbedControl = null;
            _state = State.Waiting;

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
                var listener = InputSystem.onEvent.Call(Listen);

                // Listen until we cancel or an input is grabbed
                await UniTask.WaitUntil(() => _state != State.Waiting,
                    cancellationToken: token);
                _instance._waitingText.SetActive(false);

                // Dispose
                listener?.Dispose();

                if (_possibleControls.Count == 1)
                {
                    // If there is only one option, just return that
                    MenuNavigator.Instance.PopMenu();
                    return _possibleControls[0];
                }

                // Otherwise... display the options
                _instance.RefreshList();

                // Wait until the dialog is closed
                await UniTask.WaitUntil(() => !_instance.gameObject.activeSelf,
                    cancellationToken: token);

                // Return the result
                return _grabbedControl;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }
    }
}