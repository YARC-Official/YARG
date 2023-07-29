using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using YARG.Input;
using YARG.Menu.Navigation;
using YARG.Player;

namespace YARG.Menu.Profiles
{
    public class BindHeader : NavigatableBehaviour
    {
        [Space]
        [SerializeField]
        private LocalizeStringEvent _bindingNameText;

        private EditProfileMenu _editProfileMenu;
        private YargPlayer _player;
        private ControlBinding _binding;

        public void Init(EditProfileMenu editProfileMenu, YargPlayer player, ControlBinding binding)
        {
            _editProfileMenu = editProfileMenu;
            _player = player;
            _binding = binding;

            _bindingNameText.StringReference = _binding.Name;
        }

        public async void AddNewBind()
        {
            // Select item to prevent confusion
            Selected = true;

            await _editProfileMenu.ShowControlDialog(_player, _binding);
        }
    }
}