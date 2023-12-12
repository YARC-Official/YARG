using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using YARG.Core.Input;
using YARG.Input;
using YARG.Menu.Data;
using YARG.Player;

namespace YARG.Menu.ProfileInfo
{
    public class BindHeader : MonoBehaviour
    {
        [Space]
        [SerializeField]
        private LocalizeStringEvent _bindingNameText;
        [SerializeField]
        private Image _bindingIcon;

        private EditProfileMenu _editProfileMenu;
        private YargPlayer _player;
        private ControlBinding _binding;

        public void Init(EditProfileMenu editProfileMenu, YargPlayer player, ControlBinding binding)
        {
            _editProfileMenu = editProfileMenu;
            _player = player;
            _binding = binding;

            _bindingNameText.StringReference = _binding.Name;

            var icons = MenuData.NavigationIcons;

            if (editProfileMenu.SelectingMenuBinds && icons.HasIcon((MenuAction) binding.Action))
            {
                // Show icons for menu actions
                _bindingIcon.gameObject.SetActive(true);

                _bindingIcon.sprite = icons.GetIcon((MenuAction) binding.Action);
                _bindingIcon.color = icons.GetColor((MenuAction) binding.Action);
            }
            else
            {
                // Don't for anything else
                _bindingIcon.gameObject.SetActive(false);
            }
        }

        public async void AddNewBind()
        {
            await _editProfileMenu.ShowControlDialog(_player, _binding);
        }
    }
}