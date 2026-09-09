using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.UI;
using YARG.Input.Bindings;
using YARG.Localization;

namespace YARG.Menu.ProfileList
{
    public class ReusableBindHeader : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI _bindingNameText;
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

        private BindingSetsCenterPane _centerPane;
        private ReusableBindingSet _bindingSet;
        private ReusableControlBinding _binding;

        public void Init(BindingSetsCenterPane centerPane, ReusableBindingSet bindingSet, ReusableControlBinding binding)
        {
            _centerPane = centerPane;
            _bindingSet = bindingSet;
            _binding = binding;

            _bindingNameText.text = Localize.Key(/*player.Profile.LeftyFlip TODO-FRICK: Lefty toggle
                ? binding.NameLefty
                :*/ binding.Name);

            /* TODO-FRICK: Menu binding stuff
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
            */  _bindingIcon.gameObject.SetActive(false);
            //}

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

        public void AddBinding<TSingleView, TBinding, TSingle>(
            TSingleView viewPrefab,
            TBinding binding,
            TSingle control,
            List<InputControlLayout.ControlItem> controls
        )
            where TSingleView : ReusableSingleBindView<TBinding, TSingle>
            where TBinding : ReusableControlBinding<TSingle>
            where TSingle : ReusableSingleBinding
        {
            var bindView = _bindingList.AddNewWithoutRebuild(viewPrefab);
            bindView.Init(binding, control, controls);
        }

        public void RebuildBindingsLayout()
        {
            _bindingList.RebuildLayout();
        }

        public async void AddNewBind()
        {
            // await _centerPane.ShowControlDialog(_bindingSet, _binding); TODO-FRICK
        }
    }
}
