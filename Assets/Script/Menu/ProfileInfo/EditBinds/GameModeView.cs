using TMPro;
using UnityEngine;
using YARG.Core;
using YARG.Localization;
using YARG.Menu.Navigation;

namespace YARG.Menu.ProfileInfo
{
    public class GameModeView : NavigatableBehaviour
    {
        [Space]
        [SerializeField]
        private TextMeshProUGUI _gameModeName;

        private EditBindsTab _editBindsTab;

        private GameMode _gameMode;
        private bool _isMenuBindings;

        public void Init(GameMode gameMode, EditBindsTab editBindsTab)
        {
            _editBindsTab = editBindsTab;

            _gameMode = gameMode;
            _isMenuBindings = false;

            _gameModeName.text = gameMode.ToLocalizedName();
        }

        public void InitAsMenuBindings(EditBindsTab editBindsTab)
        {
            _editBindsTab = editBindsTab;

            _isMenuBindings = true;

            _gameModeName.text = Localize.Key("Enum.GameMode.Menu");
        }

        protected override void OnSelectionChanged(bool selected)
        {
            base.OnSelectionChanged(selected);

            if (!selected)
            {
                return;
            }

            if (_isMenuBindings)
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