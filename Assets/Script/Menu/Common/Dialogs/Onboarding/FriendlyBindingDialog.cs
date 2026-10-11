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
using YARG.Helpers;
using YARG.Input;
using YARG.Input.Bindings;
using YARG.Localization;
using YARG.Menu.Data;
using YARG.Menu.Persistent;
using YARG.Menu.ProfileList;
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

        private InputDevice _controller;
        protected InputDevice Controller
        {
            get => _controller;
            set
            {
                _controller = value;
                if (value is not null) {
                    _controllerFamily = LayoutHelper.LayoutStringToControllerFamily(value.layout);
                }
            }
        }
        protected ControllerFamily      _controllerFamily;
        protected ReusableBindingSet    _bindingSet;
        protected ColoredButton         _startButton;
        protected ColoredButton         _cancelButton;

        protected GameMode _mode;

        protected CancellationTokenSource _bindingTokenSource;
        protected State                   _state;

        protected bool _lefty;

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

        public void SetParameters((ReusableBindingSet bindingSet, InputDevice controller, bool lefty) parameters)
        {
            Controller = parameters.controller;
            _bindingSet = parameters.bindingSet;
            _lefty = parameters.lefty;
        }

        public async void OnStartButtonPressed()
        {
            // If we're done, this is now a done button that should close the dialog
            if (_state == State.Done)
            {
                DialogManager.Instance.ClearDialog();
                return;
            }

            var gameMode = _bindingSet.Mode;

            // Dim the start button, make it inactive, then call the binding loop
            var button = _startButton.gameObject.GetComponentInChildren<Button>();
            button.interactable = false;
            button.image.color = Color.gray;

            _cancelButton.Text.text = Localize.Key("Menu.Dialog.FriendlyBindingDialog.Skip");
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

            var template = ReusableBindingSetTemplates.GetTemplate(mode);

            foreach (var action in template.Values)
            {
                _state = State.Waiting;
                // Get the key highlight
                var key = _lefty ? action.LeftyLocalizationKey : action.Key;

                CheckForModeSwitch(key, mode);

                // Skip unnecessary bindings and those not relevant to this dialog
                if (!action.QuickBind || !IsKeyValid(mode, key))
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
                        await InputControlBindingHelper.Instance.GetControl(Controller, _bindingTokenSource.Token, action.Type);
                }
                catch (OperationCanceledException)
                {
                    return false;
                }

                if (possibleControls.Count == 0 && !_bindingTokenSource.IsCancellationRequested)
                {
                    YargLogger.LogWarning($"Failed to bind {action.Key}");
                    continue;
                }

                if (possibleControls.Count > 0)
                {
                    // For now we just take the first thing actuated
                    // TODO: Make that more robust; the Record interface allows for selecting from multiple options

                    var path = BindingSetHelper.TrimControllerName(possibleControls[0], Controller);

                    switch (action.Type)
                    {
                        case BindingType.Button or BindingType.Impulse:
                            var buttonBinding = _bindingSet.Bindings[action.Key] as ReusableButtonBinding;
                            buttonBinding.ClearBindings();
                            var buttonConfig = new ReusableSingleButtonBindingConfig(_controllerFamily, path);
                            buttonBinding.AddBinding(new ReusableSingleButtonBinding(buttonConfig));
                            break;
                        case BindingType.Axis:
                            var axisBinding = _bindingSet.Bindings[action.Key] as ReusableAxisBinding;
                            axisBinding.ClearBindings();
                            var axisConfig = new ReusableSingleAxisBindingConfig(_controllerFamily, path);
                            axisBinding.AddBinding(new ReusableSingleAxisBinding(axisConfig));
                            break;
                        case BindingType.Integer:
                            var integerBinding = _bindingSet.Bindings[action.Key] as ReusableIntegerBinding;
                            integerBinding.ClearBindings();
                            integerBinding.AddBinding(new ReusableSingleIntegerBinding());
                            break;
                        default:
                            YargLogger.LogError("Unexpected binding type");
                            continue;
                    }
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

            YargLogger.LogWarning($"Unsupported game mode for friendly binding: {_bindingSet.Mode}");
            return null;
        }

        public void OnCancelButtonPressed()
        {
            if (_state is not State.Starting and not State.Done)
            {
                _bindingTokenSource?.Cancel();
                return;
            }

            // Close the dialog
            DialogManager.Instance.ClearDialog();
        }

        protected override void OnBeforeClose()
        {
            if (_state is not State.Starting and not State.Done)
            {
                _bindingTokenSource?.Cancel();
            }
            _state = State.Done;

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