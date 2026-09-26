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
        protected List<ControlItemInfo> _dropdownControls = new();

        protected ControlItemInfo? _current;
        protected ProfilesMenu _profilesMenu;
        protected BindingSetsCenterPane _centerPane;
        protected DummyControllerQuickBindDialogMenu _quickBindDialog;

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

            PopulateControlDropdown();

            var selectedIndex = _dropdownControls.FindIndex(i =>
                string.Equals(i.ControlPath, singleBinding.ControlPath, StringComparison.OrdinalIgnoreCase)
            );

            _controlDropdown.value = selectedIndex >= 0 ? selectedIndex + 1 : 0; // Account for the None option
            OnControlDropdownChange();
            _controlDropdown.interactable = interactable;
            _deleteButton.interactable = interactable;
            _recordButton.interactable = interactable;
            
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
            }
            else
            {
                // The -1 corrects for the presence of the None option
                _current = _dropdownControls[_controlDropdown.value - 1];
                SingleBinding.ControlPath = _current.Value.ControlPath;
            }
        }

        public void OnDelete()
        {
            DeleteRequested?.Invoke(SingleBinding);
        }

        public async void OnRecord()
        {
            if (_centerPane.DummyController is not null) {
                if(await _quickBindDialog.Show(_centerPane.DummyController, SingleBinding))
                {
                    var idx = _dropdownControls.FindIndex(c => c.ControlPath == SingleBinding.ControlPath);

                    _controlDropdown.value = idx < 0 ? 0 : idx + 1;
                }
            }
        }

        protected virtual string DisambiguateDisplayName(ControlItemInfo item)
        {
            return item.DisplayName;
        }
    }
}
