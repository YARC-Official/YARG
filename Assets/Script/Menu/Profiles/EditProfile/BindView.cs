using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using YARG.Input;

namespace YARG.Menu.Profiles
{
    public class BindView : NavigatableBehaviour
    {
        [Space]
        [SerializeField]
        private TextMeshProUGUI _bindText;

        private EditProfileMenu _editProfileMenu;
        private ControlBinding _binding;
        private InputControl _control;

        public void Init(EditProfileMenu editProfileMenu, ControlBinding binding, InputControl control)
        {
            _editProfileMenu = editProfileMenu;
            _binding = binding;
            _control = control;

            _bindText.text = $"<font-weight=400>{control.device.displayName}</font-weight> - " +
                $"<font-weight=600>{control.displayName}</font-weight>";
        }

        public void DeleteBinding()
        {
            _binding.RemoveControl(_control);
            _editProfileMenu.RefreshBindings();
        }
    }
}