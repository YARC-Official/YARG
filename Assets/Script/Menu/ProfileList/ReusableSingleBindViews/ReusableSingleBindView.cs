using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem.Layouts;
using YARG.Helpers;
using YARG.Input.Bindings;

namespace YARG.Menu.ProfileList
{
    public class ReusableSingleBindView<TBinding, TSingle> : MonoBehaviour
        where TBinding : ReusableControlBinding<TSingle>
        where TSingle : ReusableSingleBinding
    {
        [Space]
        [SerializeField]
        protected TMP_Dropdown _controlDropdown;

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

            var selectedIndex = _dropdownControls.FindIndex(i => string.Equals(i.ControlPath, singleBinding.ControlPath, StringComparison.OrdinalIgnoreCase));

            _controlDropdown.value = selectedIndex >= 0 ? selectedIndex + 1 : 0; // Account for the None option
        }

        public void DeleteBinding()
        {
            // Binding.RemoveBinding(SingleBinding); TODO-FRICK
        }

        protected virtual void PopulateControlDropdown()
        {
            _controlDropdown.options.Clear();
            _dropdownControls.Clear();
            _controlDropdown.options.Add(new("<i>None</i>"));
        }
    }
}
