using Cysharp.Text;
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

        [Space]
        [SerializeField]
        private GameObject _scoreContainer;
        [SerializeField]
        private TextMeshProUGUI _bandScore;
        [SerializeField]
        private StarView _starView;

        public void OnClick()
        {
            ViewType.ViewClick();
        }

        public override void Show(bool selected, ViewType viewType)
        {
            base.Show(selected, viewType);

            // Adjust height based on what is displayed
            if (viewType is CategoryViewType)
            {
                gameObject.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 70);
            }
            else
            {
                gameObject.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 105);
            }

            var gameInfo = viewType.GetGameInfo();

            // Show the stats
            if (gameInfo is not null)
            {
                _scoreContainer.SetActive(true);

                using var builder = ZString.CreateStringBuilder();
                builder.AppendFormat("<mspace=.5em>{0:N0}</mspace>", gameInfo.Value.BandScore);
                _bandScore.text = builder.ToString();
                _starView.SetStars(gameInfo.Value.BandStars);
            }
            else
            {
                _scoreContainer.SetActive(false);
            }
        }
    }
}