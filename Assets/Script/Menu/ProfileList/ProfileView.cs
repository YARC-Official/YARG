using System.Collections.Generic;
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
using YARG.Menu.Navigation;
using YARG.Menu.Persistent;
using YARG.Player;

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

        private ProfileListMenu _profileListMenu;
        private ProfileSidebar _profileSidebar;


        public void Init(ProfileListMenu menu, YargProfile profile, ProfileSidebar sidebar)
        {
            _profileListMenu = menu;
            _profileSidebar = sidebar;
            UpdateDisplay(profile);
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

            _profilePicture.sprite = profile.IsBot ? _profileBotSprite : _profileGenericSprite;
        }

        protected override void OnSelectionChanged(bool selected)
        {
            base.OnSelectionChanged(selected);

            if (selected)
            {
                _profileSidebar.UpdateSidebar(Profile, this);
            }
        }

        public async void RemoveProfile()
        {
            bool remove = false;

            // Confirm that the user wants to delete the profile first, UNLESS it's a bot
            if (!Profile.IsBot)
            {
                var dialog = DialogManager.Instance.ShowConfirmDeleteDialog(
                    "Deleting this profile is permanent and you will lose all stats and binds. Play history will " +
                    "remain and can be accessed in the <b>History</b> tab.", () =>
                    {
                        remove = true;
                    }, Profile.Name);

                // Wait...
                await dialog.WaitUntilClosed();
            }
            else
            {
                remove = true;
            }

            if (!remove) return;

            // Then remove

            if (Selected)
            {
                _profileSidebar.HideContents();
            }

            if (PlayerContainer.RemoveProfile(Profile))
            {
                Destroy(gameObject);
            }
        }

        public async UniTask<bool> PromptAddDevice()
        {
            var dialog = DialogManager.Instance.ShowList("Add Device\n" +
                "<alpha=#44><size=65%><line-height=50%>\nIf your device does not show up, try hitting a button/pad on " +
                "it first, and then retry.</size>");
            var player = PlayerContainer.GetPlayerFromProfile(Profile);

            bool devicesAvailable = false;
            bool selectedDevice = false;
            bool xinputDialogShowing = false;

            // Add available devices
            foreach (var device in InputSystem.devices)
            {
                if (!device.enabled) continue;
                if (PlayerContainer.IsDeviceTaken(device)) continue;

                devicesAvailable = true;
                dialog.AddListButton(device.displayName, async () =>
                {
                    player.Bindings.AddDevice(device);
                    if (!player.Bindings.ContainsBindingsForDevice(device))
                    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                        // Some remappers and non-gamepad devices show up as XInput gamepads
                        if (device is XInputController xinput)
                        {
                            xinputDialogShowing = true;
                            // Prompt user for what kind of device this is
                            var mode = await PromptGamepadMode(xinput);
                            // Skip if the gamepad is no longer present
                            if (!mode.HasValue || !xinput.added)
                            {
                                return;
                            }

                            player.Bindings.SetDefaultBinds(xinput, mode.Value);
                        }
                        else
#endif
                        {
                            player.Bindings.SetDefaultBinds(device);
                        }
                    }

                    selectedDevice = true;
                });
            }

            // Add available microphones
            foreach (var microphone in GlobalAudioHandler.GetAllInputDevices())
            {
                devicesAvailable = true;
                dialog.AddListButton(microphone.name, () =>
                {
                    var device = GlobalAudioHandler.CreateDevice(microphone.id, microphone.name);
                    player.Bindings.AddMicrophone(device);
                    selectedDevice = true;
                });
            }

            if (devicesAvailable)
            {
                await dialog.WaitUntilClosed();
                // We may be showing the xinput selection dialog, in which case we need to wait for that, too

                if (xinputDialogShowing)
                {
                    // The dialog isn't actually showing yet, so we yield for a frame
                    await UniTask.Yield();
                    await DialogManager.Instance.WaitUntilCurrentClosed();
                    // And we have to wait one more frame after it closed so that selectedDevice will actually be set
                    await UniTask.Yield();
                }

                // Update active players to hide the "No input device" icons if appropriate.
                StatsManager.Instance.UpdateActivePlayers();
            }
            else
            {
                DialogManager.Instance.ClearDialog();
            }

            return selectedDevice;
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
            dialog.AddListButton("WiitarThing Guitar", () => mode = GamepadBindingMode.WiitarThing_Guitar);
            dialog.AddListButton("WiitarThing Drumkit", () => mode = GamepadBindingMode.WiitarThing_Drums);
            dialog.AddListButton("RB4InstrumentMapper Guitar", () => mode = GamepadBindingMode.RB4InstrumentMapper_Guitar);
            dialog.AddListButton("RB4InstrumentMapper GHL Guitar", () => mode = GamepadBindingMode.RB4InstrumentMapper_GHLGuitar);
            dialog.AddListButton("RB4InstrumentMapper Drumkit", () => mode = GamepadBindingMode.RB4InstrumentMapper_Drums);
            await dialog.WaitUntilClosed();

            if (mode.HasValue)
            {
                // Cache so we only prompt once
                _xinputGamepads[xinput] = mode.Value;
            }

            return mode;
        }

        public async UniTask<bool> PromptRemoveDevice()
        {
            var dialog = DialogManager.Instance.ShowListWithSettings("Remove Device");
            var player = PlayerContainer.GetPlayerFromProfile(Profile);

            bool devicesAvailable = false;
            bool selectedDevice = false;
            bool clearBinds = false;

            dialog.AddToggleSetting("Clear Binds for Device", false, (value) => clearBinds = value);

            // Add available devices
            foreach (var device in InputSystem.devices)
            {
                if (!player.Bindings.ContainsDevice(device)) continue;

                devicesAvailable = true;
                dialog.AddListButton(device.displayName, () =>
                {
                    if (clearBinds)
                    {
                        player.Bindings.ClearBindingsForDevice(device);

                        // Remove cleared XInput devices from prompt cache
                        if (device is XInputController xinput)
                        {
                            _xinputGamepads.Remove(xinput);
                        }
                    }

                    player.Bindings.RemoveDevice(device);
                    selectedDevice = true;
                });
            }

            // Add the microphone (there should be only one or zero)
            var mic = player.Bindings.Microphone;
            if (mic is not null)
            {
                devicesAvailable = true;
                dialog.AddListButton(mic.DisplayName, () =>
                {
                    player.Bindings.RemoveMicrophone();
                    selectedDevice = true;
                });
            }

            if (devicesAvailable)
            {
                await dialog.WaitUntilClosed();
                // Update active players to show the "No input device" icons if appropriate.
                StatsManager.Instance.UpdateActivePlayers();
            }
            else
            {
                DialogManager.Instance.ClearDialog();
            }

            return selectedDevice;
        }

        public void ConnectButtonAction()
        {
            Connect(true).Forget();
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

            if (!Profile.IsBot && player.Bindings.Empty)
            {
                // Prompt the user to select a device
                if (!await PromptAddDevice())
                {
                    // Don't leak player when cancelling
                    PlayerContainer.DisposePlayer(player);
                    return;
                }
            }

            _profileListMenu.RefreshList(Profile);
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
            _profileListMenu.RefreshList();
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