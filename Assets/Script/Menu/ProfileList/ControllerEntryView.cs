using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using YARG.Core;
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
        private TMP_Dropdown _gameplayBindingSetDropdown;
        [SerializeField]
        private TMP_Dropdown _menuBindingSetDropdown;

        private YargProfile _profile;
        private ProfileView _profileView;
        private ProfileCenterPane _profileSidebar;
        private InputDevice _controller;

        private List<ReusableBindingSet> _gameplayBindingSetsByIndex;
        private List<ReusableBindingSet> _menuBindingSetsByIndex;

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
            var family = LayoutHelper.InputDeviceToControllerFamily(_controller);

            _gameplayBindingSetsByIndex = BindingsContainer.GetBindingSetsForControllerInMode(family, _profile.GameMode);
            _gameplayBindingSetDropdown.options.Clear();
            foreach (var bindingSet in _gameplayBindingSetsByIndex)
            {
                _gameplayBindingSetDropdown.options.Add(new(bindingSet.Name));
            }

            _menuBindingSetsByIndex = BindingsContainer.GetBindingSetsForControllerInMode(family, GameMode.Menu);
            _menuBindingSetDropdown.options.Clear();
            foreach (var bindingSet in _menuBindingSetsByIndex)
            {
                _menuBindingSetDropdown.options.Add(new(bindingSet.Name));
            }
        }
    }
}
