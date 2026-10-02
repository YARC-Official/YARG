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
using YARG.Localization;
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
        [SerializeField]
        private TextMeshProUGUI _gameplayBindingsTitle;
        [SerializeField]
        private TextMeshProUGUI _menuBindingsTitle;
        [SerializeField]
        private Button _jumpToGameplayBindingsButton;
        [SerializeField]
        private Button _jumpToMenuBindingsButton;

        public YargProfile Profile { get; private set; }
        private ProfileView _profileView;
        private ProfileCenterPane _centerPane;
        public InputDevice Controller { get; private set; }

        private List<ReusableBindingSet> _gameplayBindingSetsByIndex = new();
        private List<ReusableBindingSet> _menuBindingSetsByIndex = new();

        public ReusableBindingSet? GameplayBindingSet => _gameplayBindingSetsByIndex[_gameplayBindingSetDropdown.value];
        public ReusableBindingSet? MenuBindingSet => _menuBindingSetsByIndex[_menuBindingSetDropdown.value];

        private bool _open;

        public void Init(YargProfile profile, ProfileView profileView, ProfileCenterPane centerPane, InputDevice controller)
        {
            _name.text = controller.displayName;
            Profile = profile;
            _profileView = profileView;
            _centerPane = centerPane;
            Controller = controller;
            PopulateDropdownOptions();
            SetDrawerOpen(false);

            _gameplayBindingsTitle.text = $"{Localize.Key("Menu.ProfileList.BindingsTitle", Profile.GameMode)}:";
            _menuBindingsTitle.text = $"{Localize.Key("Menu.ProfileList.BindingsTitle.Menu")}:";
        }

        public void Remove()
        {
            var player = PlayerContainer.GetPlayerFromProfile(Profile);
            player.DeviceInfo.RemoveController(Controller);
            _centerPane.UpdateCenterPane(Profile, _profileView);
        }

        private void PopulateDropdownOptions()
        {
            var player = PlayerContainer.GetPlayerFromProfile(Profile);

            var family = LayoutHelper.InputDeviceToControllerFamily(Controller);

            _gameplayBindingSetsByIndex.Clear();
            _gameplayBindingSetsByIndex.Add(null); // Always start with the None option
            _gameplayBindingSetsByIndex.AddRange(BindingsContainer.GetBindingSetsForControllerInMode(family, Profile.GameMode));
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
            UpdateGameplayBindingsStatus();

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
            UpdateMenuBindingsStatus();
        }

        public void ChangeGameplayBindingSet()
        {
            var player = PlayerContainer.GetPlayerFromProfile(Profile);
            player.DeviceInfo.SelectGameplayBindingsForController(Controller, _gameplayBindingSetsByIndex[_gameplayBindingSetDropdown.value]);
            UpdateGameplayBindingsStatus();
        }

        public void ChangeMenuBindingSet()
        {
            var player = PlayerContainer.GetPlayerFromProfile(Profile);
            player.DeviceInfo.SetActiveMenuBindingsForController(Controller, _menuBindingSetsByIndex[_menuBindingSetDropdown.value]);
            UpdateMenuBindingsStatus();
        }

        private void UpdateGameplayBindingsStatus()
        {
            var status = _gameplayBindingSetDropdown.value is not 0;
            _gameplayIndicator.SetLit(status);
            _jumpToGameplayBindingsButton.interactable = status;
        }

        private void UpdateMenuBindingsStatus()
        {
            var status = _menuBindingSetDropdown.value is not 0;
            _menuIndicator.SetLit(status);
            _jumpToMenuBindingsButton.interactable = status;
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
            _layoutElement.preferredHeight = _open ? 240 : 60;
        }

        public void JumpToGameplayBindingSet()
        {
            _centerPane.ProfileMenu.JumpToBindingSet(GameplayBindingSet, Controller, Profile.LeftyFlip);
        }

        public void JumpToMenuBindingSet()
        {
            _centerPane.ProfileMenu.JumpToBindingSet(MenuBindingSet, Controller, Profile.LeftyFlip);
        }
    }
}
