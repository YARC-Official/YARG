using System.Linq;
using TMPro;
using UnityEngine;
using YARG.Core;
using YARG.Player;

namespace YARG.Menu
{
    public class ProfileView : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI _profileName;

        private YargProfile _profile;

        public void Init(YargProfile profile)
        {
            _profile = profile;

            _profileName.text = profile.Name;

            if (!PlayerContainer.IsProfileTaken(profile))
            {
                _profileName.text += " (LOGGED OUT)";
            }
        }

        public void RemoveProfile()
        {
            if (PlayerContainer.RemoveProfile(_profile))
            {
                Destroy(gameObject);
            }
        }

        public async void LoginOrLogout()
        {
            var player = PlayerContainer.GetPlayerFromProfile(_profile);

            if (player is not null)
            {
                PlayerContainer.DisposePlayer(player);
                Init(_profile);
            }
            else
            {
                // Prompt the user to select a device
                var device = await InputDeviceDialog.ShowDialog();
                if (device == null) return;

                // Create a player from the profile (and return if failed)
                player = PlayerContainer.CreatePlayerFromProfile(_profile);
                if (player is null) return;

                // Re-initialize the ProfileView
                Init(_profile);
            }
        }
    }
}
