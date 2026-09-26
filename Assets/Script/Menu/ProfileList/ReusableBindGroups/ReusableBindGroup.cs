using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.Layouts;
using YARG.Core.Logging;
using YARG.Helpers;
using YARG.Input.Bindings;
using YARG.Menu.Settings;

namespace YARG.Menu.ProfileList
{
    public abstract class ReusableBindGroup<TSingleView, TBinding, TSingle, TSingleState> : MonoBehaviour
        where TSingleView : ReusableSingleBindView<TBinding, TSingle, TSingleState>
        where TBinding : ReusableControlBinding<TSingle, TSingleState>
        where TSingle : ReusableSingleBinding<TSingleState>
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

        public virtual void Init(
            ProfilesMenu profilesMenu,
            ReusableBindingSet bindingSet,
            TBinding binding,
            List<ControlItemInfo> controls
        )
        {
            _profilesMenu = profilesMenu;

            Binding = binding;
            _controls = controls;

            _header.Init(binding, bindingSet.IsHardcoded);
            _header.BindingsClicked += ToggleBindingsDrawer;
            _header.SettingsClicked += ToggleSettingsDrawer;

            _bindingList.SetDrawerWithoutRebuild(true);
            _settingsList.SetDrawerWithoutRebuild(false);
            _header.SetSettingsButtonActive(false);
            _header.SetArrowOpen(true);

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

        public abstract void AddNewBinding();

        protected void AddBindingView(TSingle control)
        {
            var bindView = _bindingList.AddNewWithoutRebuild(_viewPrefab);
            bindView.Init(Binding, control, _controls, _profilesMenu);

            bindView.DeleteRequested += DeleteBinding;
        }

        protected virtual void DeleteBinding(TSingle control)
        {
            Binding.Bindings.Remove(control);
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
    }
}
