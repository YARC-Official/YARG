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
        }

        public void RemoveProfile()
        {
            ProfileContainer.RemoveProfile(_profile);
            Destroy(gameObject);
        }
    }
}
