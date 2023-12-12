using UnityEngine;
using UnityEngine.Localization.Components;
using YARG.Core;
using YARG.Helpers;
using YARG.Menu.Navigation;

namespace YARG.Menu.ProfileInfo
{
    public class GameModeView : NavigatableBehaviour
    {
        [Space]
        [SerializeField]
        private LocalizeStringEvent _gameModeName;

        private EditProfileMenu _editProfileMenu;

        private GameMode _gameMode;
        private bool _isMenu;

        public void Init(GameMode gameMode, EditProfileMenu editProfileMenu)
        {
            _editProfileMenu = editProfileMenu;

            _gameMode = gameMode;
            _isMenu = false;

            _gameModeName.StringReference = LocaleHelper.StringReference($"GameMode.{gameMode}");
        }

        public void InitAsMenu(EditProfileMenu editProfileMenu)
        {
            _editProfileMenu = editProfileMenu;

            _isMenu = true;

            _gameModeName.StringReference = LocaleHelper.StringReference("GameMode.Menu");
        }

        protected override void OnSelectionChanged(bool selected)
        {
            base.OnSelectionChanged(selected);

            if (_isMenu)
            {
                _editProfileMenu.RefreshMenuBindings();
            }
            else
            {
                _editProfileMenu.RefreshBindings(_gameMode);
            }
        }
    }
}