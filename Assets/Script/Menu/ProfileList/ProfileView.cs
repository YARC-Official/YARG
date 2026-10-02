using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XInput;
using UnityEngine.UI;
using YARG.Core.Audio;
using YARG.Core.Game;
using YARG.Core.Logging;
using YARG.Input;
using YARG.Localization;
using YARG.Menu;
using YARG.Menu.Data;
using YARG.Menu.Dialogs;
using YARG.Menu.Navigation;
using YARG.Menu.Persistent;
using YARG.Player;
using YARG.Settings.Metadata;

namespace YARG.Menu.ProfileList
{
    public class ProfileView : NavigatableBehaviour
    {
        // Cache for gamepads that have been prompted for this session
        private static readonly Dictionary<XInputController, GamepadBindingMode> _xinputGamepads = new();

        [Space]
        [SerializeField]
        private TextMeshProUGUI _profileName;
        [SerializeField]
        private Image _profilePicture;

        [Space]
        [SerializeField]
        private GameObject _connectGroup;
        [SerializeField]
        private GameObject _disconnectGroup;

        [Space]
        [SerializeField]
        private Button _moveUpButton;
        [SerializeField]
        private Button _moveDownButton;

        [Space]
        [SerializeField]
        private Sprite _profileGenericSprite;
        [SerializeField]
        private Sprite _profileBotSprite;

        public YargProfile Profile { get; private set; }

        /// <summary>The set-aside record this view represents, or null for a normal profile row.</summary>
        public PlayerContainer.UnloadedProfile UnloadedRecord { get; private set; }

        private ProfilesMenu _profileListMenu;
        private ProfileCenterPane  _profileCenterPane;

        public void Init(ProfilesMenu menu, YargProfile profile, ProfileCenterPane centerPane)
        {
            _profileListMenu = menu;
            _profileCenterPane = centerPane;
            UpdateDisplay(profile);
        }

        /// <summary>
        /// Shows a set-aside profile record that this version of the game could
        /// not load. The row is inert except for its delete button.
        /// </summary>
        public void InitUnloaded(ProfilesMenu menu, PlayerContainer.UnloadedProfile record, ProfileCenterPane sidebar)
        {
            _profileListMenu = menu;
            _profileCenterPane = sidebar;
            UnloadedRecord = record;

            _profileName.text = Localize.KeyFormat("Menu.ProfileList.UnloadedEntry", record.Name);

            // No connecting, disconnecting, or reordering an unloaded record. The
            // connect group starts inactive in the prefab and also contains the
            // row's delete button, so activate it and hide only the connect button
            _connectGroup.SetActive(true);
            _disconnectGroup.SetActive(false);
            var connectButton = _connectGroup.GetComponentInChildren<ColoredButton>();
            if (connectButton != null)
            {
                connectButton.gameObject.SetActive(false);
            }

            _moveUpButton.interactable = false;
            _moveDownButton.interactable = false;

            _profilePicture.sprite = _profileGenericSprite;
        }

        public void UpdateDisplay(YargProfile profile)
        {
            Profile = profile;
            _profileName.text = profile.Name;

            bool taken = PlayerContainer.IsProfileTaken(profile);
            _connectGroup.SetActive(!taken);
            _disconnectGroup.SetActive(taken);

            if (taken)
            {
                var player = PlayerContainer.GetPlayerFromProfile(profile);
                int index = PlayerContainer.GetPlayerIndex(player);

                // Disable the transition when changing interactability to prevent weird fades
                // when moving the profiles up and down.
                var upOriginal = DisableButtonTransition(_moveUpButton);
                var downOriginal = DisableButtonTransition(_moveDownButton);

                _moveUpButton.interactable = index > 0;
                _moveDownButton.interactable = index < PlayerContainer.Players.Count - 1;

                // Make sure to set the transitions back to normal afterwards
                _moveUpButton.colors = upOriginal;
                _moveDownButton.colors = downOriginal;
            }
            else if (!_profileListMenu.CanConnectProfile)
            {
                // Make the connect button gray if we have reached the connected profile cap
                var connectButton = _connectGroup.GetComponentInChildren<ColoredButton>();
                connectButton.DisableButton();
            }
            else
            {
                // In case the button was disabled before
                var connectButton = _connectGroup.GetComponentInChildren<ColoredButton>();
                connectButton.EnableButton();
            }

            _profilePicture.sprite = profile.IsBot ? _profileBotSprite : _profileGenericSprite;
        }

        protected override void OnSelectionChanged(bool selected)
        {
            base.OnSelectionChanged(selected);

            if (selected)
            {
                // Unloaded records have nothing to show in the sidebar
                if (UnloadedRecord is not null)
                {
                    _profileCenterPane.HideContents();
                    return;
                }

                _profileCenterPane.UpdateCenterPane(Profile, this);
            }
        }

        public async void RemoveProfile()
        {
            if (UnloadedRecord is not null)
            {
                RemoveUnloadedProfile();
                return;
            }

            // Bots currently delete instantly; give them the delayed-confirmation
            // dialog too so they can't be removed by an accidental click
            if (Profile.IsBot)
            {
                PresetSubTab.ShowCompactConfirmation(
                    Localize.KeyFormat("Menu.Dialog.ConfirmDelete.Title", Profile.Name),
                    Localize.Key("Menu.ProfileList.BotDelete"),
                    "Menu.Common.Delete", MenuData.Colors.CancelButton, () =>
                    {
                        DialogManager.Instance.ClearDialog();
                        RemoveNow();
                    },
                    cancelColor: MenuData.Colors.BrightButton,
                    armDelaySeconds: 2f);
                return;
            }

            bool remove = false;

            // Confirm that the user wants to delete the profile first by typing its name
            var dialog = DialogManager.Instance.ShowConfirmDeleteDialog(
                "Deleting this profile is permanent and you will lose all stats and binds. Play history will " +
                "remain and can be accessed in the <b>History</b> tab.", () => { remove = true; }, Profile.Name);

            // Wait...
            await dialog.WaitUntilClosed();

            if (!remove) return;

            RemoveNow();
        }

        private void RemoveNow()
        {
            if (Selected)
            {
                _profileCenterPane.HideContents();
            }

            if (PlayerContainer.RemoveProfile(Profile))
            {
                // Rebuild the list so emptied group headers disappear immediately
                _profileListMenu.RefreshProfileList();
            }
        }

        private void RemoveUnloadedProfile()
        {
            // Delayed-confirmation dialog (the button arms after a short delay);
            // the section header and row description already explain why the
            // profile couldn't load, so the message stays short
            PresetSubTab.ShowCompactConfirmation(
                Localize.Key("Menu.ProfileList.UnloadedDeleteTitle"),
                Localize.Key("Menu.ProfileList.UnloadedDelete"),
                "Menu.Common.Delete", MenuData.Colors.CancelButton, () =>
                {
                    DialogManager.Instance.ClearDialog();

                    if (Selected)
                    {
                        _profileCenterPane.HideContents();
                    }

                    if (PlayerContainer.DeleteUnloadedProfile(UnloadedRecord))
                    {
                        // Rebuild the list so an emptied "Couldn't Load" group's
                        // header goes away immediately
                        _profileListMenu.RefreshProfileList();
                    }
                },
                cancelColor: MenuData.Colors.BrightButton,
                armDelaySeconds: 2f);
        }

        public async UniTask<bool> PromptAddController()
        {
            var dialog = DialogManager.Instance.ShowList("Add Controller\n" +
                "<alpha=#44><size=65%><line-height=50%>\nIf your controller does not show up, try hitting a button/pad on " +
                "it first, and then retry.</size>");
            var player = PlayerContainer.GetPlayerFromProfile(Profile);

            bool selectedController = false;
            bool xinputDialogShowing = false;
            int controllerCount = 0;

            // Add InputSystem devices immediately — fast, no probe
            foreach (var controller in InputSystem.devices)
            {
                if (!controller.enabled) continue;
                if (PlayerContainer.IsDeviceTaken(controller)) continue;

                controllerCount++;
                dialog.AddListButton(controller.displayName, async () =>
                {
                    player.DeviceInfo.AddController(controller);
                    if (!player.DeviceInfo.ContainsBindingsForController(controller))
                    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                        if (controller is XInputController xinput)
                        {
                            xinputDialogShowing = true;
                            var mode = await PromptGamepadMode(xinput);
                            if (!mode.HasValue || !xinput.added)
                            {
                                return;
                            }

                            player.DeviceInfo.SetDefaultBinds(xinput, mode.Value);
                        }
                        else
#endif
                        {
                            player.DeviceInfo.SetDefaultBinds(controller);
                        }
                    }

                    selectedController = true;
                });
            }

            await dialog.WaitUntilClosed();

            if (xinputDialogShowing)
            {
                await UniTask.Yield();
                await DialogManager.Instance.WaitUntilCurrentClosed();
                await UniTask.Yield();
            }

            StatsManager.Instance.UpdateActivePlayers();

            return selectedController;
        }

        public async UniTask<bool> PromptAddMicrophone()
        {
            var dialog = DialogManager.Instance.ShowList("Add Microphone");
            var player = PlayerContainer.GetPlayerFromProfile(Profile);

            bool selectedMicrophone = false;
            bool xinputDialogShowing = false;
            int inputDeviceCount = 0;

            bool TryAddMicrophone(InputDeviceInfo device)
            {
                var created = GlobalAudioHandler.CreateInputDevice(device);
                if (created is null)
                {
                    YargLogger.LogFormatWarning("Failed to initialize microphone `{0}`.", device.DisplayName);
                    DialogManager.Instance.ClearDialog();
                    DialogManager.Instance.ShowMessage("Microphone Error",
                        $"Failed to initialize microphone:\n\n{device.DisplayName}\n\nPlease try again or choose a different microphone.");
                    return false;
                }

                player.DeviceInfo.AddMicrophone(created);
                return true;
            }

            PopulateMicsAsync(dialog, inputDeviceCount, mic =>
            {
                if (TryAddMicrophone(mic))
                {
                    selectedMicrophone = true;
                }
            }).Forget();

            await dialog.WaitUntilClosed();

            if (xinputDialogShowing)
            {
                await UniTask.Yield();
                await DialogManager.Instance.WaitUntilCurrentClosed();
                await UniTask.Yield();
            }

            StatsManager.Instance.UpdateActivePlayers();

            return selectedMicrophone;
        }

        private static async UniTask PopulateMicsAsync(
            ListDialog dialog,
            int inputDeviceCount,
            Action<InputDeviceInfo> onMicSelected)
        {
            var placeholderButton = dialog.AddListButton("Scanning microphones...", () => { }, false);
            placeholderButton.DisableButton();
            var ct = dialog.GetCancellationTokenOnDestroy();

            List<InputDeviceInfo> mics;
            try
            {
                mics = await UniTask.RunOnThreadPool(() => GlobalAudioHandler.GetAllInputDevices(),
                    cancellationToken: ct);
                await UniTask.SwitchToMainThread(cancellationToken: ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (placeholderButton != null)
            {
                Destroy(placeholderButton.gameObject);
            }

            // Dialog may have been closed while microphones were being probed
            if (!dialog.IsOpen)
            {
                return;
            }

            foreach (var mic in mics)
            {
                var localMic = mic;
                dialog.AddListButton(localMic.DisplayName, () => onMicSelected(localMic));
            }

            if (inputDeviceCount == 0 && mics.Count == 0)
            {
                var btn = dialog.AddListButton("No devices found", () => { }, false);
                btn.DisableButton();
            }
        }

        private static async UniTask<GamepadBindingMode?> PromptGamepadMode(XInputController xinput)
        {
            await DialogManager.Instance.WaitUntilCurrentClosed();

            // Check if this gamepad has been prompted for already
            if (_xinputGamepads.TryGetValue(xinput, out var existing))
            {
                return existing;
            }

            GamepadBindingMode? mode = null;

            var dialog = DialogManager.Instance.ShowList("Which kind of controller is this?");
            dialog.AddListButton("Gamepad", () => mode = GamepadBindingMode.Gamepad);
            dialog.AddListButton("CRKD Guitar (Mode 1)", () => mode = GamepadBindingMode.CrkdGuitar_Mode1);
            dialog.AddListButton("CRKD Guitar (Mode 1, FW3.0+)", () => mode = GamepadBindingMode.CrkdGuitar_Mode1_Fw30);
            dialog.AddListButton("WiitarThing Guitar", () => mode = GamepadBindingMode.WiitarThing_Guitar);
            dialog.AddListButton("WiitarThing Drumkit", () => mode = GamepadBindingMode.WiitarThing_Drums);
            dialog.AddListButton("RB4InstrumentMapper Guitar",
                () => mode = GamepadBindingMode.RB4InstrumentMapper_Guitar);
            dialog.AddListButton("RB4InstrumentMapper GHL Guitar",
                () => mode = GamepadBindingMode.RB4InstrumentMapper_GHLGuitar);
            dialog.AddListButton("RB4InstrumentMapper Drumkit",
                () => mode = GamepadBindingMode.RB4InstrumentMapper_Drums);
            await dialog.WaitUntilClosed();

            if (mode.HasValue)
            {
                // Cache so we only prompt once
                _xinputGamepads[xinput] = mode.Value;
            }

            return mode;
        }

        public void ConnectButtonAction()
        {
            if (_profileListMenu.CanConnectProfile)
            {
                Connect(true).Forget();
            }
        }

        public async UniTask Connect(bool resolveDevices)
        {
            // Select item to prevent confusion (it has to be through the mouse in this case)
            SetSelected(true, SelectionOrigin.Mouse);

            if (PlayerContainer.IsProfileTaken(Profile))
            {
                YargLogger.LogFormatError("Attempted to connect already-taken profile {0}!", Profile.Name);
                return;
            }

            // Create player from profile
            var player = PlayerContainer.CreatePlayerFromProfile(Profile, resolveDevices);
            if (player is null)
            {
                YargLogger.LogFormatError("Failed to connect profile {0}!", Profile.Name);
                return;
            }

            if (!Profile.IsBot && player.DeviceInfo.HasNoDevices)
            {
                // Prompt the user to select a device
                if (!await PromptAddController())
                {
                    // Don't leak player when cancelling
                    PlayerContainer.DisposePlayer(player);
                    _profileListMenu.RefreshProfileList(Profile);
                    return;
                }
            }

            _profileListMenu.RefreshProfileList(Profile);
        }

        public void Disconnect()
        {
            // Select item to prevent confusion (it has to be through the mouse in this case)
            SetSelected(true, SelectionOrigin.Mouse);

            var player = PlayerContainer.GetPlayerFromProfile(Profile);
            if (player is null)
            {
                YargLogger.LogFormatError("Could not get player for profile {0}!", Profile.Name);
                return;
            }

            PlayerContainer.DisposePlayer(player);
            _profileListMenu.RefreshProfileList();
        }

        public void MoveUp()
        {
            _profileListMenu.MoveProfileUp(Profile);
        }

        public void MoveDown()
        {
            _profileListMenu.MoveProfileDown(Profile);
        }

        private static ColorBlock DisableButtonTransition(Button button)
        {
            var original = button.colors;

            var noFade = button.colors;
            noFade.fadeDuration = 0f;
            button.colors = noFade;

            return original;
        }
    }
}