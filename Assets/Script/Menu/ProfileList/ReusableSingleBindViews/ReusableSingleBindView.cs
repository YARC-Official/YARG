using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using YARG.Input.Bindings;

namespace YARG.Menu.ProfileList
{
    public class ReusableSingleBindView<TBinding, TSingle> : MonoBehaviour
        where TBinding : ReusableControlBinding<TSingle>
        where TSingle : ReusableSingleBinding
    {
        [Space]
        [SerializeField]
        private TextMeshProUGUI _bindText;

        protected TBinding Binding;
        protected TSingle SingleBinding;

        public virtual void Init(TBinding binding, TSingle singleBinding)
        {
            Binding = binding;
            SingleBinding = singleBinding;

            _bindText.text = // $"<font-weight=400>{control.device.displayName}</font-weight> - " + TODO-FRICK: Controller family
                $"<font-weight=600>{singleBinding.ControlName}</font-weight>";
        }

        public void DeleteBinding()
        {
            // Binding.RemoveBinding(SingleBinding); TODO-FRICK
        }
    }
}
