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

        public void Init(GameMode gameMode, EditBindsTab editBindsTab)
        {
            _editBindsTab = editBindsTab;

            _gameMode = gameMode;

            _gameModeName.text = gameMode.ToLocalizedName();
        }

        protected override void OnSelectionChanged(bool selected)
        {
            base.OnSelectionChanged(selected);

            if (!selected)
            {
                return;
            }

            _editBindsTab.RefreshBindings(_gameMode);
        }
    }
}