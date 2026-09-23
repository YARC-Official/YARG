using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem.Layouts;
using YARG.Core.Logging;
using YARG.Helpers;
using YARG.Input.Bindings;

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

        public event Action<TSingle> DeleteRequested;

        protected TBinding Binding;
        protected TSingle SingleBinding;
        protected List<ControlItemInfo> _allControls;
        protected List<ControlItemInfo> _dropdownControls = new();

        public virtual void Init(TBinding binding, TSingle singleBinding, List<ControlItemInfo> controls)
        {
            Binding = binding;
            SingleBinding = singleBinding;
            _allControls = controls;

            PopulateControlDropdown();

            var selectedIndex = _dropdownControls.FindIndex(i =>
                string.Equals(i.ControlPath, singleBinding.ControlPath, StringComparison.OrdinalIgnoreCase)
            );

            _controlDropdown.value = selectedIndex >= 0 ? selectedIndex + 1 : 0; // Account for the None option
        }

        protected virtual void PopulateControlDropdown()
        {
            _controlDropdown.options.Clear();
            _dropdownControls.Clear();
            _controlDropdown.options.Add(new("<i>None</i>"));
        }

        public void OnControlDropdownChange()
        {
            if (_controlDropdown.value <= 0)
            {
                SingleBinding.ControlPath = null;
            }
            else
            {
                // The -1 corrects for the presence of the None option
                SingleBinding.ControlPath = _dropdownControls[_controlDropdown.value - 1].ControlPath;
            }
        }

        public void OnDelete()
        {
            DeleteRequested?.Invoke(SingleBinding);
        }

        protected virtual string DisambiguateDisplayName(ControlItemInfo item)
        {
            return item.DisplayName;
        }
    }
}
