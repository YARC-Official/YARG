using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using YARG.Core.Game;
using YARG.Player;

namespace YARG.Menu.ProfileList
{
    public class ControllerEntryView : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI _name;

        private YargProfile _profile;
        private ProfileView _profileView;
        private ProfileCenterPane _profileSidebar;
        private InputDevice _controller;

        public void Initialize(YargProfile profile, ProfileView profileView, ProfileCenterPane profileSidebar, InputDevice controller)
        {
            _name.text = controller.displayName;
            _profile = profile;
            _profileView = profileView;
            _profileSidebar = profileSidebar;
            _controller = controller;
        }

        public void Remove()
        {
            var player = PlayerContainer.GetPlayerFromProfile(_profile);
            player.DeviceInfo.RemoveController(_controller);
            _profileSidebar.UpdateCenterPane(_profile, _profileView);
        }
    }
}
