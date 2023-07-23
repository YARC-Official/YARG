using TMPro;
using UnityEngine;
using YARG.Core;

namespace YARG.Menu
{
    public class ProfileSidebar : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI _profileName;
        [SerializeField]
        private GameObject _contents;

        private YargProfile _profile;

        public void UpdateSidebar(YargProfile profile)
        {
            _profile = profile;
            _contents.SetActive(true);

            _profileName.text = _profile.Name;
        }

        public void HideContents()
        {
            _contents.SetActive(false);
        }
    }
}