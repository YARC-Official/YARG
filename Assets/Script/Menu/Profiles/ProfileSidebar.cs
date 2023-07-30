using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Settings;
using YARG.Core;
using YARG.Core.Game;
using YARG.Helpers;
using YARG.Player;

namespace YARG.Menu.Profiles
{
    public class ProfileSidebar : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI _profileName;
        [SerializeField]
        private TMP_InputField _nameInput;
        [SerializeField]
        private TMP_Dropdown _gameModeDropdown;
        [SerializeField]
        private GameObject _contents;

        [Space]
        [SerializeField]
        private GameObject _nameContainer;
        [SerializeField]
        private GameObject _editNameContainer;

        [Space]
        [SerializeField]
        private ProfilesMenu _profileMenu;

        private ProfileView _profileView;
        private YargProfile _profile;

        private readonly List<GameMode> _gameModesByIndex = new();

        private void Awake()
        {
            // Setup dropdown items
            _gameModeDropdown.options.Clear();
            foreach (var gameMode in EnumExtensions<GameMode>.Values)
            {
                // Skip vocals. It can be assigned to a profile separately.
                if (gameMode == GameMode.Vocals)
                {
                    return;
                }

                _gameModesByIndex.Add(gameMode);

                // Create the dropdown option
                string name = LocaleHelper.LocalizeString($"GameMode.{gameMode}");
                _gameModeDropdown.options.Add(new TMP_Dropdown.OptionData(name));
            }
        }

        public void UpdateSidebar(YargProfile profile, ProfileView profileView)
        {
            _profile = profile;
            _profileView = profileView;

            _contents.SetActive(true);

            // Display the profile's options
            _profileName.text = _profile.Name;
            _gameModeDropdown.value = _gameModesByIndex.IndexOf(profile.GameMode);

            // Show the proper name container (hide the editing version)
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
                _profileView.Init(_profileMenu, _profile, this);
            }
        }

        public void EditProfile()
        {
            // Only allow profile editing if it's taken
            if (!PlayerContainer.IsProfileTaken(_profile)) return;

            EditProfileMenu.CurrentProfile = _profile;
            MenuManager.Instance.PushMenu(MenuManager.Menu.EditProfile);
        }

        public void ChangeGameMode()
        {
            _profile.GameMode = _gameModesByIndex[_gameModeDropdown.value];
        }
    }
}