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

        private EditBindsTab _editBindsTab;

        private GameMode _gameMode;
        private bool _isMenu;

        public void Init(GameMode gameMode, EditBindsTab editBindsTab)
        {
            _editBindsTab = editBindsTab;

            _gameMode = gameMode;
            _isMenu = false;

            _gameModeName.StringReference = LocaleHelper.StringReference($"GameMode.{gameMode}");
        }

        public void InitAsMenu(EditBindsTab editBindsTab)
        {
            _editBindsTab = editBindsTab;

            _isMenu = true;

            _gameModeName.StringReference = LocaleHelper.StringReference("GameMode.Menu");
        }

        protected override void OnSelectionChanged(bool selected)
        {
            base.OnSelectionChanged(selected);

            if (_isMenu)
            {
                _editBindsTab.RefreshMenuBindings();
            }
            else
            {
                _editBindsTab.RefreshBindings(_gameMode);
            }
        }
    }
}