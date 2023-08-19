using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Core.Game;
using YARG.Menu.Navigation;
using YARG.Player;

namespace YARG.Menu.Profiles
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

        private ProfilesMenu _profileMenu;
        private ProfileSidebar _profileSidebar;
        private YargProfile _profile;

        public void Init(ProfilesMenu menu, YargProfile profile, ProfileSidebar sidebar)
        {
            _profileMenu = menu;
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
            Init(_profileMenu, _profile, _profileSidebar);
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

        public void RemoveProfile()
        {
            if (Selected)
            {
                _profileSidebar.HideContents();
            }

            if (PlayerContainer.RemoveProfile(_profile))
            {
                Destroy(gameObject);
                NavigationGroup.RemoveNavigatable(this);
            }
        }

        public async void ConnectButtonAction()
        {
            await Connect(true);
        }

        public async UniTask Connect(bool resolveDevices)
        {
            // Select item to prevent confusion (it has to be through the mouse in this case)
            SetSelected(true, SelectionOrigin.Mouse);

            if (PlayerContainer.IsProfileTaken(_profile))
            {
                Debug.LogError($"Attempted to connect already-taken profile {_profile.Name}!");
                return;
            }

            // Create player from profile
            var player = PlayerContainer.CreatePlayerFromProfile(_profile, resolveDevices);
            if (player is null)
            {
                Debug.LogError($"Failed to connect profile {_profile.Name}!");
                return;
            }

            if (!_profile.IsBot && player.Bindings.Empty)
            {
                // Prompt the user to select a device
                var device = await _profileMenu.ShowDeviceDialog();
                if (device is null)
                {
                    // Don't leak player when cancelling
                    PlayerContainer.DisposePlayer(player);
                    return;
                }

                // Then, add the device to the bindings
                player.Bindings.AddDevice(device);
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
                Debug.LogError($"Could not get player for profile {_profile.Name}!");
                return;
            }

            PlayerContainer.DisposePlayer(player);
            Reinitialize();
        }
    }
}
