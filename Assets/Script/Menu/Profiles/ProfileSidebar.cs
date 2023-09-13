using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Core;
using YARG.Core.Extensions;
using YARG.Core.Game;
using YARG.Helpers;
using YARG.Player;
using YARG.Settings.Customization;

namespace YARG.Menu.Profiles
{
    public class ProfileSidebar : MonoBehaviour
    {
        private const string NUMBER_FORMAT = "0.0###";

        [SerializeField]
        private GameObject _contents;
        [SerializeField]
        private TextMeshProUGUI _profileName;
        [SerializeField]
        private TMP_InputField _nameInput;
        [SerializeField]
        private Image _profilePicture;
        [SerializeField]
        private Button _editProfileButton;

        [Space]
        [SerializeField]
        private TMP_Dropdown _gameModeDropdown;
        [SerializeField]
        private TMP_InputField _noteSpeedField;
        [SerializeField]
        private TMP_InputField _highwayLengthField;
        [SerializeField]
        private Toggle _leftyFlipToggle;
        [SerializeField]
        private TMP_Dropdown _colorProfileDropdown;
        [SerializeField]
        private TMP_Dropdown _cameraPresetDropdown;

        [Space]
        [SerializeField]
        private GameObject _nameContainer;
        [SerializeField]
        private GameObject _editNameContainer;

        [Space]
        [SerializeField]
        private ProfilesMenu _profileMenu;

        [Space]
        [SerializeField]
        private Sprite _profileGenericSprite;
        [SerializeField]
        private Sprite _profileBotSprite;

        private ProfileView _profileView;
        private YargProfile _profile;

        private readonly List<GameMode> _gameModesByIndex = new();

        private List<Guid> _colorProfilesByIndex;
        private List<Guid> _cameraPresetsByIndex;

        private void Awake()
        {
            // Setup dropdown items
            _gameModeDropdown.options.Clear();
            foreach (var gameMode in EnumExtensions<GameMode>.Values)
            {
                // Skip vocals. It can be assigned to a profile separately.
                if (gameMode == GameMode.Vocals)
                {
                    continue;
                }

                _gameModesByIndex.Add(gameMode);

                // Create the dropdown option
                string name = LocaleHelper.LocalizeString($"GameMode.{gameMode}");
                _gameModeDropdown.options.Add(new(name));
            }
        }

        private void OnEnable()
        {
            // These things can change, so do it every time it's enabled.

            // Setup preset dropdowns
            _colorProfilesByIndex =
                CustomContentManager.ColorProfiles.AddOptionsToDropdown(_colorProfileDropdown)
                    .Select(i => i.Id).ToList();
            _cameraPresetsByIndex =
                CustomContentManager.CameraSettings.AddOptionsToDropdown(_cameraPresetDropdown)
                    .Select(i => i.Id).ToList();
        }

        public void UpdateSidebar(YargProfile profile, ProfileView profileView)
        {
            _profile = profile;
            _profileView = profileView;

            _contents.SetActive(true);

            // Display the profile's options
            _profileName.text = _profile.Name;
            _gameModeDropdown.value = _gameModesByIndex.IndexOf(profile.GameMode);
            _noteSpeedField.text = profile.NoteSpeed.ToString(NUMBER_FORMAT, CultureInfo.CurrentCulture);
            _highwayLengthField.text = profile.HighwayLength.ToString(NUMBER_FORMAT, CultureInfo.CurrentCulture);
            _leftyFlipToggle.isOn = profile.LeftyFlip;

            // Update preset dropdowns
            _colorProfileDropdown.value = _colorProfilesByIndex.IndexOf(profile.ColorProfile);
            _cameraPresetDropdown.value = _cameraPresetsByIndex.IndexOf(profile.CameraPreset);

            // Show the proper name container (hide the editing version)
            _nameContainer.SetActive(true);
            _editNameContainer.SetActive(false);

            // Display the proper profile picture
            _profilePicture.sprite = profile.IsBot ? _profileBotSprite : _profileGenericSprite;

            // Enable/disable the edit profile button
            _editProfileButton.interactable = !_profile.IsBot && PlayerContainer.IsProfileTaken(_profile);
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

        public async void ConnectManuallyOrAddNewDevice()
        {
            if (!PlayerContainer.IsProfileTaken(_profile))
            {
                // Just do the same as the auto connect button, but don't resolve devices.
                await _profileView.Connect(false);
            }
            else
            {
                // If it's taken, then prompt to add a new device
                var device = await _profileMenu.ShowDeviceDialog();
                if (device is null)
                {
                    return;
                }

                // Then, add the device to the bindings
                var player = PlayerContainer.GetPlayerFromProfile(_profile);
                if (player.Bindings.ContainsDevice(device))
                {
                    player.Bindings.OnDeviceAdded(device);
                }
                else
                {
                    player.Bindings.AddDevice(device);
                }
            }
        }

        public void ChangeGameMode()
        {
            _profile.GameMode = _gameModesByIndex[_gameModeDropdown.value];
        }

        public void ChangeNoteSpeed()
        {
            if (float.TryParse(_noteSpeedField.text, out var speed))
            {
                _profile.NoteSpeed = Mathf.Clamp(speed, 0f, 100f);
            }

            // Always format it after
            _noteSpeedField.text = _profile.NoteSpeed.ToString(NUMBER_FORMAT, CultureInfo.CurrentCulture);
        }

        public void ChangeHighwayLength()
        {
            if (float.TryParse(_highwayLengthField.text, out var speed))
            {
                _profile.HighwayLength = Mathf.Clamp(speed, 0.1f, 10f);
            }

            // Always format it after
            _highwayLengthField.text = _profile.HighwayLength.ToString(NUMBER_FORMAT, CultureInfo.CurrentCulture);
        }

        public void ChangeLeftyFlip()
        {
            _profile.LeftyFlip = _leftyFlipToggle.isOn;
        }

        public void ChangeColorProfile()
        {
            _profile.ColorProfile = _colorProfilesByIndex[_colorProfileDropdown.value];
        }

        public void ChangeCameraPreset()
        {
            _profile.CameraPreset = _cameraPresetsByIndex[_cameraPresetDropdown.value];
        }
    }
}