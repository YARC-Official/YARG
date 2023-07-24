using TMPro;
using UnityEngine;
using YARG.Core;

namespace YARG.Menu.Profiles
{
    public class ProfileSidebar : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI _profileName;
        [SerializeField]
        private TMP_InputField _nameInput;
        [SerializeField]
        private GameObject _contents;

        [Space]
        [SerializeField]
        private GameObject _nameContainer;
        [SerializeField]
        private GameObject _editNameContainer;

        private ProfileView _profileView;
        private YargProfile _profile;

        public void UpdateSidebar(YargProfile profile, ProfileView profileView)
        {
            _profile = profile;
            _profileView = profileView;

            _contents.SetActive(true);

            _profileName.text = _profile.Name;

            _nameContainer.SetActive(true);
            _editNameContainer.SetActive(false);
        }

        public void HideContents()
        {
            _contents.SetActive(false);
        }

        public void SetNameEditMode(bool editing)
        {
            _nameContainer.SetActive(!editing);
            _editNameContainer.SetActive(editing);

            if (editing)
            {
                _nameInput.text = _profile.Name;
                _nameInput.Select();
            }
            else
            {
                _profile.Name = _nameInput.text;
                _profileName.text = _profile.Name;
                _profileView.Init(_profile, this);
            }
        }
    }
}