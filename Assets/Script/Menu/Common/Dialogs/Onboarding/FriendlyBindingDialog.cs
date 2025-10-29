using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using YARG.Core;
using YARG.Core.Logging;
using YARG.Input;
using YARG.Localization;
using YARG.Menu.Data;
using YARG.Menu.Persistent;
using YARG.Player;

namespace YARG.Menu.Dialogs
{
    /// <summary>
    /// A friendly dialog to help users bind keys to actions
    /// Note: The caller must call SetParameters for this to actually work
    /// </summary>
    public class FriendlyBindingDialog : ImageDialog
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

        private Dictionary<GameMode, (string initial, string complete)> _bindingMessages = new()
        {
            { GameMode.FourLaneDrums, (
                "When a pad/cymbal is highlighted, strike the corresponding input on your drum kit.\n\nClick the Start button when you're ready to begin.",
                "Binding complete.\n\nYou will still need to manually set menu navigation bindings if you have not already."
                )
            },
            { GameMode.ProKeys, (
                "When a key is highlighted, press the corresponding key on your keyboard.\n\nClick the Start button when you're ready to begin.",
                "Binding complete.\n\nYou will still need to manually set bindings for 5 lane keys, star power activation, touch effects, and menu navigation."
                )
            }
        };

        public override void Initialize()
        {
            base.Initialize();

            var gameMode = _player.Profile.GameMode;
            Message.text = _bindingMessages[gameMode].initial;

            // Make sure all the highlights are disabled
            foreach (var key in _keyHighlights)
            {
                key.gameObject.SetActive(false);
            }

            ClearButtons();
            _startButton = AddDialogButton("Menu.Common.Start", MenuData.Colors.ConfirmButton, OnStartButtonPressed);
            _cancelButton = AddDialogButton("Menu.Common.Cancel", MenuData.Colors.CancelButton, OnCancelButtonPressed);

            _state = State.Starting;
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

            var gameMode = _player.Profile.GameMode;

            // Dim the start button, make it inactive, then call the binding loop
            var button = _startButton.gameObject.GetComponentInChildren<Button>();
            button.interactable = false;
            button.image.color = Color.gray;
            _player.Bindings.ClearBindingsForDevice(_device, false);
            // TODO: _controlBinding needs to be set for this to work
            _cancelButton.Text.text = Localize.Key("Menu.Dialog.FriendlyBindingDialog.Skip");
            var success = await BindingLoop();
            // TODO: if failed, we should show an error message of some sort
            button.interactable = true;
            button.image.color = MenuData.Colors.ConfirmButton;
            Message.text = _bindingMessages[gameMode].complete;
            _startButton.Text.text = Localize.Key("Menu.Common.Close");
            button = _cancelButton.gameObject.GetComponentInChildren<Button>();
            button.interactable = false;
            button.image.color = Color.gray;

            // TODO: after we've run out of bindings, we need to change this to a button that says test and switch to
            //  operating in reverse (they press button, on screen key highlights)
        }

        private async UniTask<bool> BindingLoop()
        {
            _state = State.Waiting;
            _grabbedControl = null;

            _bindGroupingTimer = null;
            _possibleControls.Clear();

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

                _cancellationTokenSource = new CancellationTokenSource();
                highlight.gameObject.SetActive(true);
                var bindingSuccess = await GetControl(_cancellationTokenSource.Token, bind);
                if (!bindingSuccess && !_cancellationTokenSource.IsCancellationRequested)
                {
                    YargLogger.LogWarning($"Failed to bind {bind.Name}");
                }

                // If we ended up in the done state the dialog is being destroyed, so we shouldn't
                // try to disable the input highlight
                if (_state != State.Done)
                {
                    highlight.gameObject.SetActive(false);
                }
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
            if (_state is not State.Starting and not State.Done)
            {
                _cancellationTokenSource.Cancel();
                return;
            }

            // Close the dialog
            DialogManager.Instance.ClearDialog();
        }

        protected override void OnBeforeClose()
        {
            if (_state is not State.Starting and not State.Done)
            {
                _cancellationTokenSource.Cancel();
            }
            _state = State.Done;
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
            Starting,
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