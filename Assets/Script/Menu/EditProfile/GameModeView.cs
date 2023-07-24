using TMPro;
using UnityEngine;
using YARG.Core;

namespace YARG.Menu.EditProfile
{
    public class GameModeView : NavigatableBehaviour
    {
        [SerializeField]
        private GameObject _selectionBackground;

        [Space]
        [SerializeField]
        private TextMeshProUGUI _gameModeName;

        private GameMode _gameMode;

        public void Init(GameMode gameMode)
        {
            _gameMode = gameMode;

            _gameModeName.text = gameMode.ToString();
        }

        protected override void OnSelectionChanged(bool selected)
        {
            _selectionBackground.SetActive(selected);
        }
    }
}