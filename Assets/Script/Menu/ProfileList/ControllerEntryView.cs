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

        private List<ReusableBindingSet> _gameplayBindingSetsByIndex = new();
        private List<ReusableBindingSet> _menuBindingSetsByIndex = new();

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
            var player = PlayerContainer.GetPlayerFromProfile(_profile);

            var family = LayoutHelper.InputDeviceToControllerFamily(_controller);

            _gameplayBindingSetsByIndex.Clear();
            _gameplayBindingSetsByIndex.Add(null); // Always start with the None option
            _gameplayBindingSetsByIndex.AddRange(BindingsContainer.GetBindingSetsForControllerInMode(family, _profile.GameMode));
            _gameplayBindingSetDropdown.options.Clear();

            foreach (var bindingSet in _gameplayBindingSetsByIndex)
            {
                if (bindingSet is null)
                {
                    _gameplayBindingSetDropdown.options.Add(new("<i>No gameplay bindings</i>"));
                    continue;
                }

                _gameplayBindingSetDropdown.options.Add(new(bindingSet.Name));
            }

            _gameplayBindingSetDropdown.value = _gameplayBindingSetsByIndex.IndexOf(player.DeviceInfo.GetActiveGameplayBindingsForController(_controller));

            _menuBindingSetsByIndex.Clear();
            _menuBindingSetsByIndex.Add(null); // Always start with the None option
            _menuBindingSetsByIndex.AddRange(BindingsContainer.GetBindingSetsForControllerInMode(family, GameMode.Menu));
            _menuBindingSetDropdown.options.Clear();
            foreach (var bindingSet in _menuBindingSetsByIndex)
            {
                if (bindingSet is null)
                {
                    _menuBindingSetDropdown.options.Add(new("<i>No menu bindings</i>"));
                    continue;
                }

                _menuBindingSetDropdown.options.Add(new(bindingSet.Name));
            }

            _menuBindingSetDropdown.value = _menuBindingSetsByIndex.IndexOf(player.DeviceInfo.GetActiveMenuBindingsForController(_controller));
        }

        public void ChangeGameplayBindingSet()
        {
            var player = PlayerContainer.GetPlayerFromProfile(_profile);
            player.DeviceInfo.SetActiveGameplayBindingsForController(_controller, _gameplayBindingSetsByIndex[_gameplayBindingSetDropdown.value]);
        }

        public void ChangeMenuBindingSet()
        {
            var player = PlayerContainer.GetPlayerFromProfile(_profile);
            player.DeviceInfo.SetActiveMenuBindingsForController(_controller, _menuBindingSetsByIndex[_menuBindingSetDropdown.value]);
        }
    }
}
