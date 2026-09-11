using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using YARG.Core.Game;
using YARG.Helpers;
using YARG.Input.Bindings;
using YARG.Player;

namespace YARG.Menu.ProfileList
{
    public class ControllerEntryView : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI _name;
        [SerializeField]
        private TMP_Dropdown _bindingSetDropdown;

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
            PopulateDropdownOptions();
        }

        public void Remove()
        {
            var player = PlayerContainer.GetPlayerFromProfile(_profile);
            player.DeviceInfo.RemoveController(_controller);
            _profileSidebar.UpdateCenterPane(_profile, _profileView);
        }

        private void PopulateDropdownOptions()
        {
            var _applicableBindingSets = BindingsContainer.GetBindingSetsForControllerInMode(
                LayoutHelper.InputDeviceToControllerFamily(_controller),
                _profile.GameMode
            );

            _bindingSetDropdown.options.Clear();

            foreach (var bindingSet in _applicableBindingSets)
            {
                _bindingSetDropdown.options.Add(new(bindingSet.Name));
            }
        }
    }
}
