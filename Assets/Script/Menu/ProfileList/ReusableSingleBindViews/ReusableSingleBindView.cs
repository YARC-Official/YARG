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
        protected List<ControlItemInfo> _controls;

        public virtual void Init(TBinding binding, TSingle singleBinding, List<ControlItemInfo> controls)
        {
            Binding = binding;
            SingleBinding = singleBinding;
            _controls = controls;

            PopulateControlDropdown();

            _controlDropdown.value = _controlDropdown.options.FindIndex(o => o.text == singleBinding.DisplayName); // TODO-FRICK
        }

        public void DeleteBinding()
        {
            // Binding.RemoveBinding(SingleBinding); TODO-FRICK
        }

        protected virtual void PopulateControlDropdown()
        {
            _controlDropdown.options.Clear();
            _controlDropdown.options.Add(new("<i>None</i>"));
        }
    }
}
