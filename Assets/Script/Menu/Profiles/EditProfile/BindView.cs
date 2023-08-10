using TMPro;
using UnityEngine;
using YARG.Input;
using YARG.Menu.Navigation;

namespace YARG.Menu.Profiles
{
    public abstract class BindView<TState, TParams> : NavigatableBehaviour
        where TState : struct
        where TParams : new()
    {
        [Space]
        [SerializeField]
        private TextMeshProUGUI _bindText;

        protected EditProfileMenu _editProfileMenu;
        protected ControlBinding<TState, TParams> _binding;
        protected ControlBinding<TState, TParams>.SingleBinding _singleBinding;

        public void Init(EditProfileMenu editProfileMenu, ControlBinding<TState, TParams> binding,
            ControlBinding<TState, TParams>.SingleBinding singleBinding)
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