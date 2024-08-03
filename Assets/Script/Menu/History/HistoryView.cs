using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Menu.ListMenu;

namespace YARG.Menu.History
{
    public class HistoryView : ViewObject<ViewType>
    {
        [Space]
        [SerializeField]
        private GameObject _fullContainer;
        [SerializeField]
        private GameObject _categoryContainer;

        [Space]
        [SerializeField]
        private GameObject _scoreContainer;
        [SerializeField]
        private TextMeshProUGUI _bandScore;
        [SerializeField]
        private StarView _starView;

        private Button _button;

        private void Awake()
        {
            _button = GetComponent<Button>();
        }

        public void OnClick()
        {
            ViewType.ViewClick();
        }

        public override void Show(bool selected, ViewType viewType)
        {
            base.Show(selected, viewType);

            // Show the correct container
            _fullContainer.SetActive(viewType.UseFullContainer);
            _categoryContainer.SetActive(!viewType.UseFullContainer);

            _button.interactable = true;

            var gameInfo = viewType.GetGameInfo();

            // Show the stats
            if (gameInfo is not null)
            {
                _scoreContainer.SetActive(true);

                _bandScore.text = gameInfo.Value.BandScore.ToString("N0");
                _starView.SetStars(gameInfo.Value.BandStars);
            }
            else
            {
                _scoreContainer.SetActive(false);
            }
        }

        public override void Hide()
        {
            base.Hide();

            // Use the smaller container to make the "drifts" smaller
            _fullContainer.SetActive(false);
            _categoryContainer.SetActive(true);

            _button.interactable = false;
        }
    }
}