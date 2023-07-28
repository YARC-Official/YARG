using TMPro;
using UnityEngine;
using YARG.Core;
using YARG.Menu.Navigation;

namespace YARG.Menu.Profiles
{
    public class GameModeView : NavigatableBehaviour
    {
        [Space]
        [SerializeField]
        private TextMeshProUGUI _gameModeName;

        private EditProfileMenu _editProfileMenu;
        private GameMode _gameMode;

        public void Init(GameMode gameMode, EditProfileMenu editProfileMenu)
        {
            _gameMode = gameMode;
            _editProfileMenu = editProfileMenu;

            _gameModeName.text = gameMode.ToString();
        }

        protected override void OnSelectionChanged(bool selected)
        {
            base.OnSelectionChanged(selected);

            _editProfileMenu.RefreshBindings(_gameMode);
        }
    }
}