using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem.Layouts;
using YARG.Input.Bindings;

namespace YARG.Menu.ProfileList
{
    public class ReusableSingleBindView<TBinding, TSingle> : MonoBehaviour
        where TBinding : ReusableControlBinding<TSingle>
        where TSingle : ReusableSingleBinding
    {
        [Space]
        [SerializeField]
        private TMP_Dropdown _controlDropdown;

        protected TBinding Binding;
        protected TSingle SingleBinding;
        private List<InputControlLayout.ControlItem> _controls;

        public virtual void Init(TBinding binding, TSingle singleBinding, List<InputControlLayout.ControlItem> controls)
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

        private void PopulateControlDropdown()
        {
            _controlDropdown.options.Clear();

            _controlDropdown.options.Add(new("<i>None</i>"));

            foreach (var control in _controls)
            {
                _controlDropdown.options.Add(new(control.displayName));
            }
        }
    }
}
