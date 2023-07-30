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

        public async void ConnectOrDisconnect()
        {
            // Select item to prevent confusion
            Selected = true;

            if (!_profile.IsBot)
            {
                await ConnectOrDisconnectAsPlayer();
            }
            else
            {
                ConnectOrDisconnectAsBot();
            }

            // Re-initialize self and sidebar
            Init(_profileMenu, _profile, _profileSidebar);
            _profileSidebar.UpdateSidebar(_profile, this);
        }

        private async UniTask ConnectOrDisconnectAsPlayer()
        {
            var player = PlayerContainer.GetPlayerFromProfile(_profile);

            if (player is not null)
            {
                PlayerContainer.DisposePlayer(player);
            }
            else
            {
                // Prompt the user to select a device
                var device = await _profileMenu.ShowDeviceDialog();
                if (device is null) return;

                // Create a player from the profile (and return if failed)
                player = PlayerContainer.CreatePlayerFromProfile(_profile);
                if (player is null) return;

                // Then, add the device to the bindings
                player.Bindings.AddDevice(device);
            }
        }

        private void ConnectOrDisconnectAsBot()
        {
            var player = PlayerContainer.GetPlayerFromProfile(_profile);

            if (player is not null)
            {
                PlayerContainer.DisposePlayer(player);
            }
            else
            {
                PlayerContainer.CreatePlayerFromProfile(_profile);
            }
        }
    }
}
