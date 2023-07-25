using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using YARG.Input;
using YARG.Menu.InputControlDialog;

namespace YARG.Menu.EditProfile
{
    public class BindHeader : NavigatableBehaviour
    {
        [Space]
        [SerializeField]
        private LocalizeStringEvent _bindingNameText;

        private InputDevice _inputDevice;
        private ControlBinding _binding;

        public void Init(InputDevice inputDevice, ControlBinding binding)
        {
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
            var control = await InputControlDialogMenu.Show(_inputDevice);
            if (control == null) return;

            _binding.AddControl(control);
        }
    }
}