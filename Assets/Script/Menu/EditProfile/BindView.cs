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

        private ControlBinding _binding;
        private InputControl _control;

        public void Init(ControlBinding binding, InputControl control)
        {
            _binding = binding;
            _control = control;

            _bindText.text = control.displayName;
        }
    }
}