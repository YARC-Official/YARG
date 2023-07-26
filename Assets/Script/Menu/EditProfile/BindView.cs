using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using YARG.Input;

namespace YARG.Menu.EditProfile
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

            _bindText.text = control.displayName;
        }

        public void DeleteBind()
        {
            _binding.RemoveControl(_control);
            _editProfileMenu.RefreshBindings();
        }
    }
}