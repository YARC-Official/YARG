using TMPro;
using UnityEngine;
using YARG.Input;
using YARG.Menu.Navigation;

namespace YARG.Menu.Profiles
{
    public abstract class BindView<TState, TBinding> : NavigatableBehaviour
        where TState : struct
        where TBinding : SingleBinding<TState>
    {
        [Space]
        [SerializeField]
        private TextMeshProUGUI _bindText;

        protected EditProfileMenu _editProfileMenu;
        protected ControlBinding<TState, TBinding> _binding;
        protected TBinding _singleBinding;

        public void Init(EditProfileMenu editProfileMenu, ControlBinding<TState, TBinding> binding,
            TBinding singleBinding)
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