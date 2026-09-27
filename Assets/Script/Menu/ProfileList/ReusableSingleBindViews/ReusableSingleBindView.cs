using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.UI;
using YARG.Core.Logging;
using YARG.Helpers;
using YARG.Input.Bindings;
using YARG.Menu.ProfileInfo;

namespace YARG.Menu.ProfileList
{
    public class ReusableSingleBindView<TBinding, TSingle, TSingleState> : MonoBehaviour
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

        public virtual void Init(
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

            PopulateControlDropdown();

            var selectedIndex = _dropdownControls.FindIndex(i =>
                string.Equals(i.ControlPath, singleBinding.ControlPath, StringComparison.OrdinalIgnoreCase)
            );

            _controlDropdown.value = selectedIndex >= 0 ? selectedIndex + 1 : 0; // Account for the None option
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
        }

        public void OnDelete()
        {
            DeleteRequested?.Invoke(SingleBinding);
        }

        public async void OnRecord()
        {
            if (_centerPane.DummyController is not null)
            {
                if (await _quickBindDialog.Show(_centerPane.DummyController, SingleBinding))
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
        }

        protected virtual string DisambiguateDisplayName(ControlItemInfo item)
        {
            return item.DisplayName;
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
