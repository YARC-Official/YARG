using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Core;
using YARG.Core.Game;
using YARG.Localization;
using YARG.Menu.Data;
using YARG.Menu.Persistent;
using YARG.Menu.ProfileInfo;
using YARG.Player;
using YARG.Scores;
using YARG.Settings.Customization;

namespace YARG.Menu.ProfileList
{
    // This will be cleaned up when we add the new profile overview screen

    public class ProfileSidebar : MonoBehaviour
    {
        private const string NUMBER_FORMAT = "0.0###";

        private static readonly GameMode[] _gameModes =
        {
            GameMode.FiveFretGuitar,
            GameMode.FourLaneDrums,
            GameMode.FiveLaneDrums,
            GameMode.Vocals,
            GameMode.ProKeys
        };

        [SerializeField]
        private GameObject _contents;
        [SerializeField]
        private TextMeshProUGUI _profileName;
        [SerializeField]
        private TMP_InputField _nameInput;
        [SerializeField]
        private Image _profilePicture;
        [SerializeField]
        private Button[] _profileActionButtons;

        [Space]
        [SerializeField]
        private TMP_Dropdown _gameModeDropdown;
        [SerializeField]
        private TMP_InputField _noteSpeedField;
        [SerializeField]
        private TMP_InputField _highwayLengthField;
        [SerializeField]
        private TMP_InputField _inputCalibrationField;
        [SerializeField]
        private Toggle _leftyFlipToggle;
        [SerializeField]
        private TMP_Dropdown _engineDropdown;
        [SerializeField]
        private TMP_Dropdown _themeDropdown;
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
        private ProfileListMenu _profileListMenu;

        [Space]
        [SerializeField]
        private Sprite _profileGenericSprite;
        [SerializeField]
        private Sprite _profileBotSprite;

        private ProfileView _profileView;
        private YargProfile _profile;

        private readonly List<GameMode> _gameModesByIndex = new();

        private List<Guid> _enginePresetsByIndex;
        private List<Guid> _colorProfilesByIndex;
        private List<Guid> _cameraPresetsByIndex;
        private List<Guid> _themesByIndex;

        private void Awake()
        {
            // Setup dropdown items
            _gameModeDropdown.options.Clear();
            foreach (var gameMode in _gameModes)
            {
                _gameModesByIndex.Add(gameMode);

                // Create the dropdown option
                _gameModeDropdown.options.Add(new(gameMode.ToLocalizedName()));
            }
        }

        private void OnEnable()
        {
            // These things can change, so do it every time it's enabled.

            // Setup preset dropdowns
            _enginePresetsByIndex =
                CustomContentManager.EnginePresets.AddOptionsToDropdown(_engineDropdown)
                    .Select(i => i.Id).ToList();
            _themesByIndex =
                CustomContentManager.ThemePresets.AddOptionsToDropdown(_themeDropdown)
                    .Select(i => i.Id).ToList();
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

            if (!PlayerContainer.IsProfileTaken(_profile))
            {
                _contents.SetActive(false);
                return;
            }

            _contents.SetActive(true);

            // Display the profile's options
            _profileName.text = _profile.Name;
            _gameModeDropdown.value = _gameModesByIndex.IndexOf(profile.GameMode);
            _noteSpeedField.text = profile.NoteSpeed.ToString(NUMBER_FORMAT, CultureInfo.CurrentCulture);
            _highwayLengthField.text = profile.HighwayLength.ToString(NUMBER_FORMAT, CultureInfo.CurrentCulture);
            _inputCalibrationField.text = _profile.InputCalibrationMilliseconds.ToString();
            _leftyFlipToggle.isOn = profile.LeftyFlip;

            // Update preset dropdowns
            _engineDropdown.SetValueWithoutNotify(
                _enginePresetsByIndex.IndexOf(profile.EnginePreset));
            _themeDropdown.SetValueWithoutNotify(
                _themesByIndex.IndexOf(profile.ThemePreset));
            _colorProfileDropdown.SetValueWithoutNotify(
                _colorProfilesByIndex.IndexOf(profile.ColorProfile));
            _cameraPresetDropdown.SetValueWithoutNotify(
                _cameraPresetsByIndex.IndexOf(profile.CameraPreset));

            // Show the proper name container (hide the editing version)
            _nameContainer.SetActive(true);
            _editNameContainer.SetActive(false);

            // Display the proper profile picture
            _profilePicture.sprite = profile.IsBot ? _profileBotSprite : _profileGenericSprite;

            // Enable/disable the edit profile button
            bool interactable = !_profile.IsBot && PlayerContainer.IsProfileTaken(_profile);
            foreach (var button in _profileActionButtons)
            {
                button.interactable = interactable;
            }
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
                // Set the name. Make sure to record the name change in the scores.
                _profile.Name = _nameInput.text;
                ScoreContainer.RecordPlayerInfo(_profile.Id, _profile.Name);

                // Update the UI
                _profileName.text = _profile.Name;
                _profileView.Init(_profileListMenu, _profile, this);
            }
        }

        public void EditProfile()
        {
            // Only allow profile editing if it's taken
            if (!PlayerContainer.IsProfileTaken(_profile)) return;

            var menu = MenuManager.Instance.PushMenu(MenuManager.Menu.ProfileInfo, false);

            menu.GetComponent<ProfileInfoMenu>().CurrentProfile = _profile;
            menu.gameObject.SetActive(true);
        }

        public void AddDevice()
        {
            _profileView.PromptAddDevice().Forget();
        }

        public void RemoveDevice()
        {
            _profileView.PromptRemoveDevice().Forget();
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

        public void ChangeInputCalibration()
        {
            if (long.TryParse(_inputCalibrationField.text, out long calibration))
            {
                _profile.InputCalibrationMilliseconds = calibration;
            }

            // Always format it after
            _inputCalibrationField.text = _profile.InputCalibrationMilliseconds.ToString();
        }

        public void ChangeLeftyFlip()
        {
            _profile.LeftyFlip = _leftyFlipToggle.isOn;
        }

        public void ChangeEngine()
        {
            _profile.EnginePreset = _enginePresetsByIndex[_engineDropdown.value];
        }

        public void ChangeTheme()
        {
            var themeGuid = _themesByIndex[_themeDropdown.value];

            // Skip if there are no changes
            if (themeGuid == _profile.ThemePreset) return;

            _profile.ThemePreset = themeGuid;

            var themePreset = CustomContentManager.ThemePresets.GetPresetById(themeGuid);

            bool hasPresets = false;
            var presets = string.Empty;

            // Check camera presets
            if (CustomContentManager.CameraSettings
                .TryGetPresetById(themePreset.PreferredCameraPreset, out var cameraPreset))
            {
                hasPresets = true;
                presets += $"<color=yellow>Camera Preset: {cameraPreset.Name}</color>\n";
            }

            // Check color profiles
            if (CustomContentManager.ColorProfiles
                .TryGetPresetById(themePreset.PreferredColorProfile, out var colorProfile))
            {
                hasPresets = true;
                presets += $"<color=yellow>Color Profile: {colorProfile.Name}</color>\n";
            }

            // Skip if there are no preferred presets
            if (!hasPresets) return;

            // Ask user if they'd like to apply the preferred presets
            var dialog = DialogManager.Instance.ShowMessage("Apply Recommended Presets?",
                "This theme has recommended presets. These presets will make the theme look as intended. " +
                "Would you like to apply them?\n\n" + presets.Trim());
            dialog.ClearButtons();

            // Add buttons

            dialog.AddDialogButton("Menu.Common.Cancel", MenuData.Colors.CancelButton,
                () => DialogManager.Instance.ClearDialog());

            dialog.AddDialogButton("Menu.Common.Apply", MenuData.Colors.ConfirmButton, () =>
            {
                _profile.CameraPreset = cameraPreset?.Id ?? CameraPreset.Default.Id;
                _profile.ColorProfile = colorProfile?.Id ?? ColorProfile.Default.Id;

                UpdateSidebar(_profile, _profileView);

                DialogManager.Instance.SubmitAndClearDialog();
            });
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