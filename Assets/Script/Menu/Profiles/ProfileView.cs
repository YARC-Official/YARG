using TMPro;
using UnityEngine;
using YARG.Core;
using YARG.Menu.InputDeviceDialog;
using YARG.Player;

namespace YARG.Menu.Profiles
{
    public class ProfileView : NavigatableBehaviour
    {
        [Space]
        [SerializeField]
        private TextMeshProUGUI _profileName;

        [Space]
        [SerializeField]
        private GameObject _connectGroup;
        [SerializeField]
        private GameObject _disconnectGroup;

        private ProfileSidebar _profileSidebar;
        private YargProfile _profile;

        public void Init(YargProfile profile, ProfileSidebar sidebar)
        {
            _profile = profile;
            _profileSidebar = sidebar;

            _profileName.text = profile.Name;

            bool taken = PlayerContainer.IsProfileTaken(profile);
            _connectGroup.gameObject.SetActive(!taken);
            _disconnectGroup.gameObject.SetActive(taken);
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
            var player = PlayerContainer.GetPlayerFromProfile(_profile);

            if (player is not null)
            {
                PlayerContainer.DisposePlayer(player);
                Init(_profile, _profileSidebar);
            }
            else
            {
                // Prompt the user to select a device
                var device = await InputDeviceDialogMenu.Show();
                if (device == null) return;

                // Create a player from the profile (and return if failed)
                player = PlayerContainer.CreatePlayerFromProfile(_profile);
                if (player is null) return;

                // Then, add the device to the bindings
                if (!player.Bindings.AddDevice(device)) return;

                // Re-initialize the ProfileView
                Init(_profile, _profileSidebar);
            }
        }
    }
}
