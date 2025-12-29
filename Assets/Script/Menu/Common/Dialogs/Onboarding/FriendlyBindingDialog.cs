using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
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
    public abstract class FriendlyBindingDialog : ImageDialog
    {
        [SerializeField]
        protected TextMeshProUGUI ControlText;
        [SerializeField]
        protected Image[] _keyHighlights;

        protected InputDevice   _device;
        protected YargPlayer    _player;
        protected ColoredButton _startButton;
        protected ColoredButton _cancelButton;

        protected GameMode _mode;

        protected CancellationTokenSource _cancellationTokenSource;
        protected CancellationTokenSource _bindingTokenSource;
        protected State                   _state;
        protected ActuationSettings       _bindSettings    = new();

        protected BindingCollection _bindingCollection;

        protected abstract (string initial, string complete) BindingMessages { get; set; }

        public override void Initialize()
        {
            base.Initialize();

            Message.text = BindingMessages.initial;
            ControlText.text = "";

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

        public void SetParameters((InputDevice device, YargPlayer player, GameMode mode) parameters)
        {
            _device = parameters.device;
            _player = parameters.player;
            _bindingCollection = _player.Bindings[_player.Profile.GameMode];
            _mode = parameters.mode;
        }

        public async void OnStartButtonPressed()
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

            _cancelButton.Text.text = Localize.Key("Menu.Dialog.FriendlyBindingDialog.Skip");
            _cancellationTokenSource = new CancellationTokenSource();
            bool success;
            try
            {
                success = await BindingLoop(gameMode);
            }
            catch (OperationCanceledException)
            {
                _bindingTokenSource.Cancel();
            }
            finally
            {
                ControlText.text = "";
            }

            // TODO: if failed, we should show an error message of some sort
            button.interactable = true;
            button.image.color = MenuData.Colors.ConfirmButton;
            Message.text = BindingMessages.complete;
            _startButton.Text.text = Localize.Key("Menu.Common.Close");
            button = _cancelButton.gameObject.GetComponentInChildren<Button>();
            button.interactable = false;
            button.image.color = Color.gray;

            // TODO: after we've run out of bindings, we need to change this to a button that says test and switch to
            //  operating in reverse (they press button, on screen key highlights)
        }

        protected abstract void CheckForModeSwitch(string key, GameMode mode);

        protected virtual async UniTask<bool> BindingLoop(GameMode mode)
        {
            _state = State.Waiting;

            foreach (var bind in _bindingCollection)
            {
                _state = State.Waiting;
                // Get the key highlight
                var key = _player.Profile.LeftyFlip ? bind.NameLefty : bind.Name;

                CheckForModeSwitch(key, mode);

                // Skip bindings not relevant to this dialog
                if (!IsKeyValid(mode, key))
                {
                    continue;
                }

                var highlight = GetHighlightByName(mode, key);
                if (highlight is null)
                {
                    continue;
                }

                ControlText.text = Localize.Key("Bindings", key);

                List<InputControl> possibleControls;

                try
                {
                    _bindingTokenSource = new CancellationTokenSource();
                    highlight.gameObject.SetActive(true);
                    possibleControls =
                        await InputControlBindingHelper.Instance.GetControl(_player, _bindingTokenSource.Token, bind);
                }
                catch (OperationCanceledException)
                {
                    return false;
                }

                if (possibleControls.Count == 0 && !_bindingTokenSource.IsCancellationRequested)
                {
                    YargLogger.LogWarning($"Failed to bind {bind.Name}");
                    continue;
                }

                if (possibleControls.Count > 0)
                {
                    // For now we just take the first thing actuated
                    // TODO: Make this more robust
                    bind.AddControl(_bindSettings, possibleControls[0]);
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

        protected virtual bool IsKeyValid(GameMode mode, string key)
        {
            return true;
        }

        protected virtual Image GetHighlightByName(GameMode mode, string bindingName)
        {
            // TODO: Use the action enum instead of the name

            if (mode == GameMode.ProKeys)
            {
                if (bindingName.StartsWith("ProKeys.Key"))
                {
                    var key = _keyHighlights[int.Parse(bindingName[11..]) - 1];
                    return key;
                }

                return null;
            }

            if (mode == GameMode.FourLaneDrums)
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

            if (mode == GameMode.FiveLaneDrums)
            {
                var key = bindingName switch
                {
                    "FiveDrums.RedPad"       => _keyHighlights[0],
                    "FiveDrums.YellowCymbal" => _keyHighlights[1],
                    "FiveDrums.BluePad"      => _keyHighlights[2],
                    "FiveDrums.OrangeCymbal" => _keyHighlights[3],
                    "FiveDrums.GreenPad"     => _keyHighlights[4],
                    "Drums.Kick"         => _keyHighlights[5],
                    _ => null
                };

                return key;
            }

            if (mode == GameMode.EliteDrums)
            {
                // This is confusing since we do both 4 and 5 lane with different prefabs
                var key = bindingName switch
                {
                    "EliteDrums.FourLaneRedDrum" => _keyHighlights[0],
                    "EliteDrums.FourLaneYellowDrum" => _keyHighlights[1],
                    "EliteDrums.FourLaneBlueDrum" => _keyHighlights[2],
                    "EliteDrums.FourLaneGreenDrum" => _keyHighlights[3],
                    "EliteDrums.FourLaneYellowCymbal" => _keyHighlights[4],
                    "EliteDrums.FourLaneBlueCymbal" => _keyHighlights[5],
                    "EliteDrums.FourLaneGreenCymbal" => _keyHighlights[6],

                    "Drums.Kick" => _mode == GameMode.FourLaneDrums ? _keyHighlights[7] : _keyHighlights[5],

                    "EliteDrums.FiveLaneRedDrum" => _keyHighlights[0],
                    "EliteDrums.FiveLaneBlueDrum" => _keyHighlights[2],
                    "EliteDrums.FiveLaneGreenDrum" => _keyHighlights[4],
                    "EliteDrums.FiveLaneYellowCymbal" => _keyHighlights[1],
                    "EliteDrums.FiveLaneOrangeCymbal" => _keyHighlights[3],
                    _ => null
                };

                return key;
            }

            YargLogger.LogWarning($"Unsupported game mode for friendly binding: {_player.Profile.GameMode}");
            return null;
        }

        public void OnCancelButtonPressed()
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
                _cancellationTokenSource?.Cancel();
                _bindingTokenSource?.Cancel();
            }
            _state = State.Done;

            _cancellationTokenSource?.Dispose();
            _bindingTokenSource?.Dispose();
        }

        protected enum State
        {
            Starting,
            Waiting,
            Select,
            Done
        }
    }
}