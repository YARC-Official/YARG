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

            if (!ProfileContainer.TakenProfiles.Contains(profile))
            {
                _profileName.text += " (LOGGED OUT)";
            }
        }

        public void RemoveProfile()
        {
            if (ProfileContainer.RemoveProfile(_profile))
            {
                Destroy(gameObject);
            }
        }

        public void LoginOrLogout()
        {
            if (ProfileContainer.TakenProfiles.Contains(_profile))
            {
                ProfileContainer.ReturnProfile(_profile);
                Init(_profile);
            }
            else
            {
                ProfileContainer.TakeProfile(_profile);
                Init(_profile);

                MenuNavigator.Instance.PushMenu(MenuNavigator.Menu.InputDeviceDialog);
            }
        }
    }
}
