using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using YARG.Audio;
using YARG.Core.Audio;
using YARG.Core.Game;
using YARG.Core.Logging;
using YARG.Menu.Navigation;
using YARG.Menu.Persistent;
using YARG.Player;

namespace YARG.Menu.ProfileList
{
    public class ProfileView : NavigatableBehaviour
    {
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
        private Sprite _profileGenericSprite;
        [SerializeField]
        private Sprite _profileBotSprite;

        private ProfileListMenu _profileListMenu;
        private ProfileSidebar _profileSidebar;
        private YargProfile _profile;

        public void Init(ProfileListMenu menu, YargProfile profile, ProfileSidebar sidebar)
        {
            _profileListMenu = menu;
            _profile = profile;
            _profileSidebar = sidebar;

            _profileName.text = profile.Name;

            bool taken = PlayerContainer.IsProfileTaken(profile);
            _connectGroup.gameObject.SetActive(!taken);
            _disconnectGroup.gameObject.SetActive(taken);

            _profilePicture.sprite = profile.IsBot ? _profileBotSprite : _profileGenericSprite;
        }

        private void Reinitialize()
        {
            Init(_profileListMenu, _profile, _profileSidebar);
            _profileSidebar.UpdateSidebar(_profile, this);
        }

        protected override void OnSelectionChanged(bool selected)
        {
            base.OnSelectionChanged(selected);

            if (selected)
            {
                _profileSidebar.UpdateSidebar(_profile, this);
            }
        }

        public async void RemoveProfile()
        {
            bool remove = false;

            // Confirm that the user wants to delete the profile first, UNLESS it's a bot
            if (!_profile.IsBot)
            {
                var dialog = DialogManager.Instance.ShowConfirmDeleteDialog(
                    "Deleting this profile is permanent and you will lose all stats and binds. Play history will " +
                    "remain and can be accessed in the <b>History</b> tab.", () =>
                    {
                        remove = true;
                    }, _profile.Name);

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

            if (PlayerContainer.RemoveProfile(_profile))
            {
                Destroy(gameObject);
            }
        }

        public async UniTask<bool> PromptAddDevice()
        {
            var dialog = DialogManager.Instance.ShowList("Add Device");
            var player = PlayerContainer.GetPlayerFromProfile(_profile);

            bool devicesAvailable = false;
            bool selectedDevice = false;

            // Add available devices
            foreach (var device in InputSystem.devices)
            {
                if (!device.enabled) continue;
                if (PlayerContainer.IsDeviceTaken(device)) continue;

                devicesAvailable = true;
                dialog.AddListButton(device.displayName, () =>
                {
                    player.Bindings.AddDevice(device);
                    selectedDevice = true;
                    DialogManager.Instance.ClearDialog();
                });
            }

            // Add available microphones
            foreach (var microphone in AudioManager.Instance.GetAllInputDevices())
            {
                devicesAvailable = true;
                dialog.AddListButton(microphone.name, () =>
                {
                    var device = AudioManager.Instance.CreateDevice(microphone.id, microphone.name);
                    player.Bindings.AddMicrophone(device);
                    selectedDevice = true;
                    DialogManager.Instance.ClearDialog();
                });
            }

            if (devicesAvailable)
            {
                await dialog.WaitUntilClosed();
            }
            else
            {
                DialogManager.Instance.ClearDialog();
            }

            return selectedDevice;
        }

        public async UniTask<bool> PromptRemoveDevice()
        {
            var dialog = DialogManager.Instance.ShowList("Remove Device");
            var player = PlayerContainer.GetPlayerFromProfile(_profile);

            bool devicesAvailable = false;
            bool selectedDevice = false;

            // Add available devices
            foreach (var device in InputSystem.devices)
            {
                if (!player.Bindings.ContainsDevice(device)) continue;

                devicesAvailable = true;
                dialog.AddListButton(device.displayName, () =>
                {
                    player.Bindings.RemoveDevice(device);
                    selectedDevice = true;
                    DialogManager.Instance.ClearDialog();
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
                    DialogManager.Instance.ClearDialog();
                });
            }

            if (devicesAvailable)
            {
                await dialog.WaitUntilClosed();
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

            if (PlayerContainer.IsProfileTaken(_profile))
            {
                YargLogger.LogFormatError("Attempted to connect already-taken profile {0}!", _profile.Name);
                return;
            }

            // Create player from profile
            var player = PlayerContainer.CreatePlayerFromProfile(_profile, resolveDevices);
            if (player is null)
            {
                YargLogger.LogFormatError("Failed to connect profile {0}!", _profile.Name);
                return;
            }

            if (!_profile.IsBot && player.Bindings.Empty)
            {
                // Prompt the user to select a device
                if (!await PromptAddDevice())
                {
                    // Don't leak player when cancelling
                    PlayerContainer.DisposePlayer(player);
                    return;
                }
            }

            Reinitialize();
        }

        public void Disconnect()
        {
            // Select item to prevent confusion (it has to be through the mouse in this case)
            SetSelected(true, SelectionOrigin.Mouse);

            var player = PlayerContainer.GetPlayerFromProfile(_profile);
            if (player is null)
            {
                YargLogger.LogFormatError("Could not get player for profile {0}!", _profile.Name);
                return;
            }

            PlayerContainer.DisposePlayer(player);
            Reinitialize();
        }
    }
}