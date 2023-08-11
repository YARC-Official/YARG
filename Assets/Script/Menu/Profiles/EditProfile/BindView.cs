using TMPro;
using UnityEngine;
using YARG.Input;
using YARG.Menu.Navigation;

namespace YARG.Menu.Profiles
{
    public abstract class BindView<TState, TBinding, TSingle> : NavigatableBehaviour
        where TState : struct
        where TBinding : ControlBinding<TState, TSingle>
        where TSingle : SingleBinding<TState>
    {
        [Space]
        [SerializeField]
        private TextMeshProUGUI _bindText;

        protected EditProfileMenu _editProfileMenu;
        protected TBinding _binding;
        protected TSingle _singleBinding;

        public virtual void Init(EditProfileMenu editProfileMenu, TBinding binding, TSingle singleBinding)
        {
            _editProfileMenu = editProfileMenu;
            _binding = binding;
            _singleBinding = singleBinding;

            var control = singleBinding.Control;
            _bindText.text = $"<font-weight=400>{control.device.displayName}</font-weight> - " +
                $"<font-weight=600>{control.displayName}</font-weight>";
        }

        public void DeleteBinding()
        {
            _binding.RemoveBinding(_singleBinding);
        }
    }
}