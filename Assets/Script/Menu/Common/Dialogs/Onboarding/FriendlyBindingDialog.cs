using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using YARG.Core;
using YARG.Core.Logging;
using YARG.Input;
using YARG.Menu.Data;
using YARG.Menu.Persistent;
using YARG.Menu.ProfileInfo;
using YARG.Player;

namespace YARG.Menu.Dialogs
{
    /// <summary>
    /// A friendly dialog to help users bind keys to actions
    /// Note: The caller must call SetDevice for this to actually work
    /// </summary>
    public class FriendlyBindingDialog : DiscreteProgressDialog
    {
        // I can't really think of a better way to do this than have key highlights defined and positioned in the editor
        [SerializeField]
        private Image[] _keyHighlights;

        private InputDevice   _device;
        private YargPlayer    _player;
        private ColoredButton _startButton;
        private ColoredButton _cancelButton;

        // TODO: Refactor this so that InputControlDialogMenu and this can share
        //  duplicated code is bad.....mmkay?
        // Be sure to cancel this task when the dialog closes
        private UniTask<bool>           _bindingTask;
        private CancellationTokenSource _cancellationTokenSource;
        private CancellationToken       _bindingToken;
        private State                   _state;
        private ControlBinding          _binding;
        private AllowedControl          _allowedControls = AllowedControl.All;
        private ActuationSettings       _bindSettings    = new();
        private InputControl            _grabbedControl;

        private          float?             _bindGroupingTimer;
        private readonly List<InputControl> _possibleControls = new();

        private BindingCollection _bindingCollection;

        public override void Initialize()
        {
            SetSteps(_keyHighlights.Length);
            base.Initialize();

            Message.text =
                "Press the highlighted key on your keyboard.\n\nPress the Start button when you're ready to begin.";

            // Make sure all the highlights are disabled
            foreach (var key in _keyHighlights)
            {
                key.gameObject.SetActive(false);
            }

            ClearButtons();
            _startButton = AddDialogButton("Start", MenuData.Colors.ConfirmButton, OnStartButtonPressed);
            _cancelButton = AddDialogButton("Cancel", MenuData.Colors.CancelButton, OnCancelButtonPressed);
        }

        public void SetParameters((InputDevice device, YargPlayer player) parameters)
        {
            _device = parameters.device;
            _player = parameters.player;
            _bindingCollection = _player.Bindings[_player.Profile.GameMode];
        }

        private async void OnStartButtonPressed()
        {
            // If we're done, this is now a done button that should close the dialog
            if (_state == State.Done)
            {
                DialogManager.Instance.ClearDialog();
                return;
            }

            // Dim the start button, make it inactive, then call the binding loop
            var button = _startButton.gameObject.GetComponentInChildren<Button>();
            button.interactable = false;
            button.image.color = Color.gray;
            _player.Bindings.ClearBindingsForDevice(_device);
            // TODO: _controlBinding needs to be set for this to work
            _cancelButton.Text.text = "Skip";
            var success = await BindingLoop();
            // TODO: if failed, we should show an error message of some sort
            button.interactable = true;
            button.image.color = MenuData.Colors.ConfirmButton;
            _startButton.Text.text = "Done";
            button = _cancelButton.gameObject.GetComponentInChildren<Button>();
            button.interactable = false;
            button.image.color = Color.gray;

            // TODO: Start needs to change to skip here so that the user can skip any controls they don't have on their device
            //  after we've run out of bindings, we then need to change this to a button that says test and switch to operating
            //  in reverse (they press button, on screen key highlights) and cancel needs to become "close"
        }

        private async UniTask<bool> BindingLoop()
        {
            _state = State.Waiting;
            _grabbedControl = null;

            _bindGroupingTimer = null;
            _possibleControls.Clear();
            _cancellationTokenSource = new CancellationTokenSource();
            _bindingToken = _cancellationTokenSource.Token;

            // Ok, we need to figure out a way to convert the binding action to a button on the screen
            foreach (var bind in _bindingCollection)
            {
                _state = State.Waiting;
                // Get the key highlight
                var key = _player.Profile.LeftyFlip ? bind.NameLefty : bind.Name;
                var highlight = GetHighlightByName(key);
                if (highlight is null)
                {
                    continue;
                }
                highlight.gameObject.SetActive(true);
                var bindingSuccess = await GetControl(_bindingToken, bind);
                if (!bindingSuccess)
                {
                    YargLogger.LogWarning($"Failed to bind {bind.Name}");
                }
                highlight.gameObject.SetActive(false);
                ProgressToNextStep();
            }

            _state = State.Done;
            return true;
        }

        private async UniTask<bool> GetControl(CancellationToken token, ControlBinding binding)
        {
            _possibleControls.Clear();
            _binding = binding;

            try
            {
                // Listen until we cancel or an input is grabbed
                InputState.onChange += Listen;
                await UniTask.WaitUntil(() => _state != State.Waiting, cancellationToken: token);
                InputState.onChange -= Listen;

                // Get the actuated control
                if (_possibleControls.Count > 1)
                {
                    // Multiple controls actuated, let the user choose
                    // RefreshList();

                    // Wait until the dialog is closed
                    // await UniTask.WaitUntil(() => !gameObject.activeSelf, cancellationToken: token);
                    YargLogger.LogWarning("Multiple controls actuated, but we don't support that yet!");
                    return false;
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
                _state = State.Waiting;
                return false;
            }
            finally
            {
                _state = State.Waiting;
                InputState.onChange -= Listen;
            }
        }

        // This is virtual because different instruments will have different key highlights (eventually)
        protected virtual Image GetHighlightByName(string bindingName)
        {
            // For now we're just testing with pro keys, so I'm not going to set up a bunch of different maps
            // Pro keys uses "ProKeys.Key1" through "ProKeys.Key25" for the main keys, and that's all we're doing for now
            // TODO: Use the action enum instead of the name

            if (_player.Profile.GameMode == GameMode.ProKeys)
            {
                if (bindingName.StartsWith("ProKeys.Key"))
                {
                    var key = _keyHighlights[int.Parse(bindingName[11..]) - 1];
                    return key;
                }

                return null;
            }

            if (_player.Profile.GameMode == GameMode.FourLaneDrums)
            {
                var key = bindingName switch
                {
                    "FourDrums.RedPad" => _keyHighlights[0],
                    "FourDrums.YellowPad" => _keyHighlights[1],
                    "FourDrums.BluePad" => _keyHighlights[2],
                    "FourDrums.GreenPad" => _keyHighlights[3],
                    "FourDrums.YellowCymbal" => _keyHighlights[4],
                    "FourDrums.BlueCymbal" => _keyHighlights[5],
                    "FourDrums.GreenCymbal" => _keyHighlights[6],
                    "Drums.Kick" => _keyHighlights[7],
                    _ => null
                };

                return key;
            }

            YargLogger.LogWarning($"Unsupported game mode for friendly binding: {_player.Profile.GameMode}");
            return null;
        }

        private void OnCancelButtonPressed()
        {
            if (_bindingTask.Status == UniTaskStatus.Pending)
            {
                _cancellationTokenSource.Cancel();
                return;
            }

            // Close the dialog
            DialogManager.Instance.ClearDialog();
        }

        protected override void OnBeforeClose()
        {
            _state = State.Done;
            if (_bindingTask.Status == UniTaskStatus.Pending)
            {
                _cancellationTokenSource.Cancel();
            }
        }

        // TODO
        // All this stuff is lifted from InputControlDialogMenu..this is not OK. Figure out how to single source this
        // and use it anywhere we need to create bindings

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

        private void Listen(InputDevice device, InputEventPtr iep)
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
    }
}