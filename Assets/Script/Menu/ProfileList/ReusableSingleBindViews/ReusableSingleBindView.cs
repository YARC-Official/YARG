using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.UI;
using YARG.Core.Logging;
using YARG.Helpers;
using YARG.Input.Bindings;
using YARG.Menu.ProfileInfo;

namespace YARG.Menu.ProfileList
{
    public abstract class ReusableSingleBindView<TBinding, TSingle, TSingleState> : MonoBehaviour
        where TBinding : ReusableControlBinding<TSingle, TSingleState>
        where TSingle : ReusableSingleBinding<TSingleState>
        where TSingleState : struct
    {
        [Space]
        [SerializeField]
        protected TMP_Dropdown _controlDropdown;
        [SerializeField]
        private Button _deleteButton;
        [SerializeField]
        private Button _recordButton;

        public event Action<TSingle> DeleteRequested;

        protected TBinding Binding;
        protected TSingle SingleBinding;
        protected List<ControlItemInfo> _allControls;
        protected List<DropdownControl> _dropdownControls = new();
        private DropdownControl _adHocControl;

        protected ControlItemInfo? _current;
        protected ProfilesMenu _profilesMenu;
        protected BindingSetsCenterPane _centerPane;
        protected DummyControllerQuickBindDialogMenu _quickBindDialog;
        private bool _interactable { get; set; }

        protected InputControl _dummyInputControl;

        public virtual void Init(
            ReusableBindGroup bindGroup,
            TBinding binding,
            TSingle singleBinding,
            List<ControlItemInfo> controls,
            ProfilesMenu profilesMenu,
            BindingSetsCenterPane centerPane,
            DummyControllerQuickBindDialogMenu quickBindDialog,
            bool interactable
        )
        {
            _profilesMenu = profilesMenu;
            _centerPane = centerPane;
            _quickBindDialog = quickBindDialog;
            Binding = binding;
            SingleBinding = singleBinding;
            _allControls = controls;
            _interactable = interactable;

            bindGroup.Unlocked += OnUnlocked;

            UpdateDummyInputControl();
            PopulateControlDropdown();

            var selectedIndex = _dropdownControls.FindIndex(i =>
                string.Equals(i.ControlPath, singleBinding.ControlPath, StringComparison.OrdinalIgnoreCase)
            );

            if (selectedIndex >= 0) {
                // Known control for this controller family; set the dropdown, accounting for the None option
                _controlDropdown.value = selectedIndex + 1;
            }
            else if (!string.IsNullOrEmpty(SingleBinding.ControlPath))
            {
                // Unexpected control for this controller family; create an ad hoc option and select it
                _adHocControl = new(SingleBinding.ControlPath, SingleBinding.DisplayName, SingleBinding.SourceLayout);
                _dropdownControls.Add(_adHocControl);
                _controlDropdown.options.Add(new(_adHocControl.DisplayName));

                _controlDropdown.SetValueWithoutNotify(_controlDropdown.options.Count - 1);
                _controlDropdown.RefreshShownValue();
            }
            else
            {
                // Nothing; select None
                _controlDropdown.value = 0;
            }

            OnControlDropdownChange();


            _controlDropdown.interactable = _interactable;
            _deleteButton.interactable = _interactable;
            _recordButton.interactable = _interactable && _centerPane.DummyController is not null;

            _centerPane.DummyControllerChanged += OnDummyControllerChanged;
        }

        private void OnDisable()
        {
            _centerPane.DummyControllerChanged -= OnDummyControllerChanged;
        }

        protected virtual void PopulateControlDropdown()
        {
            _controlDropdown.options.Clear();
            _dropdownControls.Clear();
            _controlDropdown.options.Add(new("<i>None</i>"));
        }

        public virtual void OnControlDropdownChange()
        {
            if (_controlDropdown.value <= 0)
            {
                SingleBinding.ControlPath = null;
                _current = null;
                RemoveAdHocControl();
            }
            else
            {
                // The -1 corrects for the presence of the None option
                var selected = _dropdownControls[_controlDropdown.value - 1];
                SingleBinding.ControlPath = selected.ControlPath;
                _current = selected.KnownControl;

                if (selected != _adHocControl)
                {
                    RemoveAdHocControl();
                }
            }

            UpdateDummyInputControl();
        }

        private void RemoveAdHocControl()
        {
            if (_adHocControl is null)
            {
                return;
            }

            var adHocIndex = _dropdownControls.IndexOf(_adHocControl);

            if (adHocIndex >= 0)
            {
                _dropdownControls.RemoveAt(adHocIndex);
                _controlDropdown.options.RemoveAt(adHocIndex + 1);
            }

            _adHocControl = null;
        }

        private void OnDummyControllerChanged()
        {
            _recordButton.interactable = _interactable && _centerPane.DummyController is not null;
            UpdateDummyInputControl();
        }

        public void OnDelete()
        {
            DeleteRequested?.Invoke(SingleBinding);
        }

        public async void OnRecord()
        {
            if (await SingleBinding.QuickBind(_centerPane.DummyController, _quickBindDialog))
            {
                var idx = _dropdownControls.FindIndex(c =>
                            string.Equals(
                                c.ControlPath,
                                SingleBinding.ControlPath,
                                StringComparison.OrdinalIgnoreCase
                            )
                        );

                if (idx >= 0)
                {
                    _controlDropdown.value = idx + 1;
                }
                else
                {
                    RemoveAdHocControl();
                    _adHocControl = new(SingleBinding.ControlPath, SingleBinding.DisplayName, SingleBinding.SourceLayout);

                    _dropdownControls.Add(_adHocControl);
                    _controlDropdown.options.Add(
                        new(_adHocControl.DisplayName));

                    _controlDropdown.value = _controlDropdown.options.Count - 1;
                    _controlDropdown.RefreshShownValue();
                }
            }
        }

        public void OnUnlocked()
        {
            _interactable = true;
            _recordButton.interactable = _centerPane.DummyController is not null;
            _controlDropdown.interactable = true;
            _deleteButton.interactable = true;

        }

        private void Update()
        {
            if (_dummyInputControl is not null)
            {
                UpdateDummyInputVisuals(_dummyInputControl as InputControl<TSingleState>);
            }
        }

        protected virtual void UpdateDummyInputVisuals(InputControl<TSingleState> dummyInput) { }

        protected virtual string DisambiguateDisplayName(ControlItemInfo item)
        {
            return item.DisplayName;
        }

        private void UpdateDummyInputControl()
        {
            _dummyInputControl = SingleBinding?.FindControl(_centerPane.DummyController);
        }

        protected class DropdownControl
        {
            public string ControlPath { get; }
            public string DisplayName { get; }
            public string SourceLayout { get; }
            public ControlItemInfo? KnownControl { get; }

            public DropdownControl(ControlItemInfo control)
            {
                ControlPath = control.ControlPath;
                DisplayName = control.DisplayName;
                SourceLayout = control.SourceLayout;
                KnownControl = control;
            }

            public DropdownControl(string controlPath, string displayName, string sourceLayout)
            {
                ControlPath = controlPath;
                DisplayName = displayName;
                SourceLayout = sourceLayout;
                KnownControl = null;
            }
        }
    }

}
