using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XInput;
using UnityEngine.UI;
using YARG.Core.Audio;
using YARG.Core.Game;
using YARG.Core.Logging;
using YARG.Input;
using YARG.Menu;
using YARG.Menu.Dialogs;
using YARG.Menu.Navigation;
using YARG.Menu.Persistent;
using YARG.Player;

namespace YARG.Menu.ProfileList
{
    public class DeviceView : NavigatableBehaviour
    {
        // Cache for gamepads that have been prompted for this session
        private static readonly Dictionary<XInputController, GamepadBindingMode> _xinputGamepads = new();

        [Space]
        [SerializeField]
        private TextMeshProUGUI _profileName;
        [SerializeField]
        private Image _profilePicture;

        [Space]
        [SerializeField]
        private GameObject _detachGroup;

        private YargPlayer user = null;
        public InputDevice Controller { get; private set; }

        private ProfilesAndDevicesMenu _profilesAndDevicesMenu;
        private DeviceCenterPane  _deviceCenterPane;

        public void Init(ProfilesAndDevicesMenu menu, InputDevice controller, DeviceCenterPane centerPane)
        {
            _profilesAndDevicesMenu = menu;
            _deviceCenterPane = centerPane;
            UpdateDisplay(controller);
        }

        public void UpdateDisplay(InputDevice controller)
        {
            Controller = controller;
            _profileName.text = controller.displayName;

            foreach (var player in PlayerContainer.Players)
            {
                if (player.DeviceInfo.Controllers.Contains(controller))
                {
                    user = player;
                    break;
                }
            }

            _detachGroup.SetActive(user is not null);
        }

        protected override void OnSelectionChanged(bool selected)
        {
            base.OnSelectionChanged(selected);

            if (selected)
            {
                _deviceCenterPane.UpdateCenterPane(Controller, this);
            }
        }

        public async void Detach()
        {
            if (Selected)
            {
                _deviceCenterPane.HideContents();
            }

            if (user.DeviceInfo.RemoveController(Controller))
            {
                Destroy(gameObject);
            }
        }
    }
}