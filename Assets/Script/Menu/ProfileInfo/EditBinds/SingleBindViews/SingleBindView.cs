using TMPro;
using UnityEngine;
using YARG.Input;

namespace YARG.Menu.ProfileInfo
{
    public abstract class SingleBindView<TState, TBinding, TSingle> : MonoBehaviour
        where TState : struct
        where TBinding : ControlBinding<TState, TSingle>
        where TSingle : SingleBinding<TState>
    {
        [Space]
        [SerializeField]
        private TextMeshProUGUI _bindText;

        protected EditBindsTab EditBindsTab;
        protected TBinding Binding;
        protected TSingle SingleBinding;

        public virtual void Init(EditBindsTab editBindsTab, TBinding binding, TSingle singleBinding)
        {
            EditBindsTab = editBindsTab;
            Binding = binding;
            SingleBinding = singleBinding;

            var control = singleBinding.Control;
            _bindText.text = $"<font-weight=400>{control.device.displayName}</font-weight> - " +
                $"<font-weight=600>{control.displayName}</font-weight>";
        }

        public void DeleteBinding()
        {
            Binding.RemoveBinding(SingleBinding);
        }
    }
}