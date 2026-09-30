using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using YARG.Core;
using YARG.Core.Game;
using YARG.Core.Logging;
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
        [SerializeField]
        private GameObject _drawer;
        [SerializeField]
        private Transform _arrow;
        [SerializeField]
        private LayoutElement _layoutElement;
        [SerializeField]
        private BindingsIndicator _gameplayIndicator;
        [SerializeField]
        private BindingsIndicator _menuIndicator;

        private YargProfile _profile;
        private ProfileView _profileView;
        private ProfileCenterPane _profileSidebar;
        public InputDevice Controller { get; private set; }

        private List<ReusableBindingSet> _gameplayBindingSetsByIndex = new();
        private List<ReusableBindingSet> _menuBindingSetsByIndex = new();

        private bool _open;

        public void Init(YargProfile profile, ProfileView profileView, ProfileCenterPane profileSidebar, InputDevice controller)
        {
            _name.text = controller.displayName;
            _profile = profile;
            _profileView = profileView;
            _profileSidebar = profileSidebar;
            Controller = controller;
            PopulateDropdownOptions();
            SetDrawerOpen(false);
        }

        public void Remove()
        {
            var player = PlayerContainer.GetPlayerFromProfile(_profile);
            player.DeviceInfo.RemoveController(Controller);
            _profileSidebar.UpdateCenterPane(_profile, _profileView);
        }

        private void PopulateDropdownOptions()
        {
            var player = PlayerContainer.GetPlayerFromProfile(_profile);

            var family = LayoutHelper.InputDeviceToControllerFamily(Controller);

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

            _gameplayBindingSetDropdown.value = _gameplayBindingSetsByIndex.IndexOf(player.DeviceInfo.GetSelectedGameplayBindingsForController(Controller));
            UpdateGameplayIndicator();

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

            _menuBindingSetDropdown.value = _menuBindingSetsByIndex.IndexOf(player.DeviceInfo.GetActiveMenuBindingsForController(Controller));
            UpdateMenuIndicator();
        }

        public void ChangeGameplayBindingSet()
        {
            var player = PlayerContainer.GetPlayerFromProfile(_profile);
            player.DeviceInfo.SelectGameplayBindingsForController(Controller, _gameplayBindingSetsByIndex[_gameplayBindingSetDropdown.value]);
            UpdateGameplayIndicator();
        }

        public void ChangeMenuBindingSet()
        {
            var player = PlayerContainer.GetPlayerFromProfile(_profile);
            player.DeviceInfo.SetActiveMenuBindingsForController(Controller, _menuBindingSetsByIndex[_menuBindingSetDropdown.value]);
            UpdateMenuIndicator();
        }

        private void UpdateGameplayIndicator()
        {
            _gameplayIndicator.SetLit(_gameplayBindingSetDropdown.value is not 0);
        }

        private void UpdateMenuIndicator()
        {
            _menuIndicator.SetLit(_menuBindingSetDropdown.value is not 0);
        }

        public void ToggleDrawer()
        {
            SetDrawerOpen(!_open);
        }

        private void SetDrawerOpen(bool open)
        {
            _open = open;
            _drawer.SetActive(_open);
            _arrow.localScale = _arrow.localScale.WithY(_open ? 1f : -1f);
            _layoutElement.preferredHeight = _open ? 180 : 60;
        }
    }
}
