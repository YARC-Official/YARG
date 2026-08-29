using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using YARG.Assets.Script.Helpers;
using YARG.Core;
using YARG.Core.Game;
using YARG.Helpers.Extensions;
using YARG.Localization;
using YARG.Menu.Data;
using YARG.Menu.Filters;
using YARG.Menu.Main;
using YARG.Menu.Persistent;
using YARG.Menu.ProfileInfo;
using YARG.Player;
using YARG.Scores;
using YARG.Settings.Customization;
using static UnityEditor.AddressableAssets.Build.Layout.BuildLayout;

namespace YARG.Menu.ProfileList
{
    // This will be cleaned up when we add the new profile overview screen

    public class DeviceCenterPane : MonoBehaviour
    {

        [SerializeField]
        private GameObject _contents;
        [SerializeField]
        private TextMeshProUGUI _deviceName;
        [SerializeField]
        private TextMeshProUGUI _configSetName;
        [SerializeField]
        private TMP_InputField _nameInput;
        [SerializeField]
        private Image _profilePicture;

        [Space]
        [SerializeField]
        private GameObject _sidebarContent;
        
        [SerializeField]
        private TMP_InputField _inputCalibrationField;

        [Space]
        [SerializeField]
        private GameObject _nameContainer;
        [SerializeField]
        private GameObject _editNameContainer;

        [Space]
        [SerializeField]
        private ProfilesAndDevicesMenu _profilesAndDevicesMenu;

        private DeviceView _deviceView;
        private InputDevice _controller;

        private void Awake()
        {
            // Setup dropdown items (none yet)            
        }

        private void OnEnable()
        {
            // These things can change, so do it every time it's enabled.

            PopulateDropdownOptions();
        }

        private void PopulateDropdownOptions()
        {
        }

        public void UpdateCenterPane(InputDevice controller, DeviceView deviceView)
        {
            _controller = controller;
            _deviceView = deviceView;

            PopulateDropdownOptions();

            _contents.SetActive(true);

            // Display the profile's options
            _deviceName.text = _controller.displayName;

            // Show the proper name container (hide the editing version)
            _nameContainer.SetActive(true);
            _editNameContainer.SetActive(false);
        }
        public void HideContents()
        {
            _contents.SetActive(false);
        }
    }
}
