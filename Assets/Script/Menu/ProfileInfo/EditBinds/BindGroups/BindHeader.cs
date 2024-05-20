using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using YARG.Core.Input;
using YARG.Input;
using YARG.Menu.Data;
using YARG.Player;

namespace YARG.Menu.ProfileInfo
{
    public class BindHeader : MonoBehaviour
    {
        [SerializeField]
        private LocalizeStringEvent _bindingNameText;
        [SerializeField]
        private Image _bindingIcon;

        [Space]
        [SerializeField]
        private DropdownDrawer _bindingList;
        [SerializeField]
        private DropdownDrawer _settingsList;

        [Space]
        [SerializeField]
        private Button _settingsButton;
        [SerializeField]
        private GameObject _dropdownArrow;

        private EditBindsTab _editBindsTab;
        private YargPlayer _player;
        private ControlBinding _binding;

        public void Init(EditBindsTab editBindsTab, YargPlayer player, ControlBinding binding)
        {
            _editBindsTab = editBindsTab;
            _player = player;
            _binding = binding;

            if (player.Profile.LeftyFlip)
            {
                _bindingNameText.StringReference = binding.NameLefty;
            }
            else
            {
                _bindingNameText.StringReference = binding.Name;
            }

            var icons = MenuData.NavigationIcons;
            if (editBindsTab.SelectingMenuBinds && icons.HasIcon((MenuAction) binding.Action))
            {
                // Show icons for menu actions
                _bindingIcon.gameObject.SetActive(true);

                _bindingIcon.sprite = icons.GetIcon((MenuAction) binding.Action);
                _bindingIcon.color = icons.GetColor((MenuAction) binding.Action);
            }
            else
            {
                // Don't for anything else
                _bindingIcon.gameObject.SetActive(false);
            }

            _bindingList.SetDrawerWithoutRebuild(true);
            FlipArrow();
        }

        public void ToggleBindingsDrawer()
        {
            // Close settings drawer if it's opened instead of opening bindings drawer
            if (!_bindingList.DrawerOpened && _settingsList.DrawerOpened)
            {
                SetSettingsDrawer(false);
                return;
            }

            SetBindingsDrawer(!_bindingList.DrawerOpened);
        }

        public void SetBindingsDrawer(bool open)
        {
            _bindingList.DrawerOpened = open;

            if (!open)
                SetSettingsDrawer(open);

            FlipArrow();
        }

        public void ToggleSettingsDrawer() => SetSettingsDrawer(!_settingsList.DrawerOpened);

        public void SetSettingsDrawer(bool open)
        {
            var colors = _settingsButton.colors;
            colors.colorMultiplier = open ? 0.75f : 1f;
            _settingsButton.colors = colors;

            _settingsList.DrawerOpened = open;

            FlipArrow();
        }

        private void FlipArrow()
        {
            float arrowScale = _bindingList.DrawerOpened || _settingsList.DrawerOpened ? -1f : 1f;
            _dropdownArrow.transform.localScale = _dropdownArrow.transform.localScale.WithY(arrowScale);
        }

        public void ClearBindings()
        {
            _bindingList.ClearDrawer();
        }

        public void AddBinding<TView, TState, TBinding, TSingle>(TView viewPrefab, TBinding binding, TSingle control)
            where TView : SingleBindView<TState, TBinding, TSingle>
            where TState : struct
            where TBinding : ControlBinding<TState, TSingle>
            where TSingle : SingleBinding<TState>
        {
            var bindView = _bindingList.AddNewWithoutRebuild(viewPrefab);
            bindView.Init(binding, control);
        }

        public void RebuildBindingsLayout()
        {
            _bindingList.RebuildLayout();
        }

        public async void AddNewBind()
        {
            await _editBindsTab.ShowControlDialog(_player, _binding);
        }
    }
}