using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.Layouts;
using YARG.Core.Logging;
using YARG.Helpers;
using YARG.Input.Bindings;
using YARG.Menu.ProfileInfo;
using YARG.Menu.Settings;

namespace YARG.Menu.ProfileList
{
    public abstract class ReusableBindGroup : MonoBehaviour
    {
        public event Action<bool> HandednessChanged;
    }

    public abstract class ReusableBindGroup<TSingleView, TBinding, TSingle, TSingleState> : ReusableBindGroup
        where TSingleView : ReusableSingleBindView<TBinding, TSingle, TSingleState>
        where TBinding : ReusableControlBinding<TSingle, TSingleState>
        where TSingle : ReusableSingleBinding<TSingleState>, new()
        where TSingleState : struct
    {
        [SerializeField]
        protected ReusableBindHeader _header;
        [SerializeField]
        protected DropdownDrawer _bindingList;
        [SerializeField]
        protected DropdownDrawer _settingsList;
        [SerializeField]
        protected TSingleView _viewPrefab;

        public TBinding Binding { get; protected set; }

        protected List<ControlItemInfo> _controls;

        protected ProfilesMenu _profilesMenu;
        protected BindingSetsCenterPane _centerPane;
        protected DummyControllerQuickBindDialogMenu _quickBindDialog;
        protected bool _interactable;
        protected bool _showLeftyNames;

        public virtual void Init(
            ProfilesMenu profilesMenu,
            BindingSetsCenterPane centerPane,
            DummyControllerQuickBindDialogMenu quickBindDialog,
            ReusableBindingSet bindingSet,
            TBinding binding,
            List<ControlItemInfo> controls
        )
        {
            _profilesMenu = profilesMenu;
            _centerPane = centerPane;
            _quickBindDialog = quickBindDialog;
            _interactable = !bindingSet.IsHardcoded;

            Binding = binding;
            _controls = controls;

            _header.Init(this, binding, _interactable, _centerPane.ShowLeftyNames);
            _header.BindingsClicked += ToggleBindingsDrawer;
            _header.SettingsClicked += ToggleSettingsDrawer;

            _bindingList.SetDrawerWithoutRebuild(true);
            _settingsList.SetDrawerWithoutRebuild(false);
            _header.SetSettingsButtonActive(false);
            _header.SetArrowOpen(true);

            _centerPane.HandednessChanged += RefreshHandedness;

            RefreshBindings();
        }

        public void RefreshBindings()
        {
            _bindingList.ClearDrawer();

            foreach (var control in Binding.Bindings)
            {
                AddBindingView(control);
            }

            _bindingList.RebuildLayout();
        }

        public async void AddNewBinding()
        {
            TSingle newBinding = new();
            await newBinding.QuickBind(_centerPane.DummyController, _quickBindDialog); // Will return immediately if no dummy controller
            Binding.AddBinding(newBinding);
            RefreshBindings();
        }

        protected void AddBindingView(TSingle control)
        {
            var bindView = _bindingList.AddNewWithoutRebuild(_viewPrefab);
            bindView.Init(Binding, control, _controls, _profilesMenu, _centerPane, _quickBindDialog, _interactable);

            bindView.DeleteRequested += DeleteBinding;
        }

        protected virtual void DeleteBinding(TSingle control)
        {
            Binding.RemoveBinding(control);
            RefreshBindings();
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
                SetSettingsDrawer(false);

            _header.SetArrowOpen(_bindingList.DrawerOpened || _settingsList.DrawerOpened);
        }

        public void SetSettingsDrawer(bool open)
        {
            _settingsList.DrawerOpened = open;
            _header.SetSettingsButtonActive(open);

            _header.SetArrowOpen(
                _bindingList.DrawerOpened || _settingsList.DrawerOpened
            );
        }

        public void ToggleSettingsDrawer() => SetSettingsDrawer(!_settingsList.DrawerOpened);

        private void OnDestroy()
        {
            _centerPane.HandednessChanged -= RefreshHandedness;
        }

        private void RefreshHandedness(bool lefty)
        {
            _header.RefreshHandedness(lefty);
        }
    }
}
