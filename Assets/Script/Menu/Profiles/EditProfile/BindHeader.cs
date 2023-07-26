using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using YARG.Input;

namespace YARG.Menu.Profiles
{
    public class BindHeader : NavigatableBehaviour
    {
        [Space]
        [SerializeField]
        private LocalizeStringEvent _bindingNameText;

        private EditProfileMenu _editProfileMenu;
        private InputDevice _inputDevice;
        private ControlBinding _binding;

        public void Init(EditProfileMenu editProfileMenu, InputDevice inputDevice, ControlBinding binding)
        {
            _editProfileMenu = editProfileMenu;
            _inputDevice = inputDevice;
            _binding = binding;

            _bindingNameText.StringReference = new LocalizedString
            {
                TableReference = "Bindings",
                TableEntryReference = _binding.Name
            };
        }

        public async void AddNewBind()
        {
            // Select item to prevent confusion
            Selected = true;

            var control = await InputControlDialogMenu.Show(_inputDevice);
            if (control == null) return;

            _binding.AddControl(control);
            _editProfileMenu.RefreshBindings();
        }
    }
}