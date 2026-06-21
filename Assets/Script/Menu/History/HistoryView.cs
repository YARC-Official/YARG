using System.Collections.Generic;
using Cysharp.Text;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using YARG.Core;
using YARG.Helpers.Extensions;
using YARG.Menu.ListMenu;
using YARG.Scores;

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
        [SerializeField]
        private GameObject _instrumentsContainer;
        [SerializeField]
        private Image[] _instrumentIcons;
        [SerializeField]
        private TextMeshProUGUI _additionalInstrumentsText;

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

                if (gameInfo.Value.PlayerScoreRecords is not null)
                {
                    _instrumentsContainer.SetActive(true);
                    PopulatePlayerInstrumentIcons(gameInfo.Value.PlayerScoreRecords);
                }
                else
                {
                    _instrumentsContainer.SetActive(false);
                }
            }
            else
            {
                _scoreContainer.SetActive(false);
                _instrumentsContainer.SetActive(false);
            }
        }

        private void PopulatePlayerInstrumentIcons(List<PlayerScoreRecord> playerScoreRecords)
        {
            /*
             - "Instrument Icons" on the HistoryView prefab is arranged visually as:
               [5][4][3][2][1 and 0]
             - If there are 5 players or fewer, display their icons in indices 1 to n
             - For 6+ players, display number of extra players in slot 0, then use slots 2-5
               to display the icons of the first 4 players
             */
            if (playerScoreRecords.Count <= 5)
            {
                _instrumentIcons[0].enabled = false;
                _additionalInstrumentsText.enabled = false;

                for (int i = 1; i <= 5; i++)
                {
                    if (i > playerScoreRecords.Count)
                    {
                        _instrumentIcons[i].enabled = false;
                    }
                    else
                    {
                        EnableInstrumentIcon(i, playerScoreRecords[i - 1].Instrument);
                    }
                }
            }
            else
            {
                _instrumentIcons[0].enabled = true;
                _instrumentIcons[1].enabled = false;
                _additionalInstrumentsText.enabled = true;
                _additionalInstrumentsText.text = "+" + (playerScoreRecords.Count - 4);

                for (int i = 2; i <= 5; i++)
                {
                    EnableInstrumentIcon(i, playerScoreRecords[i - 2].Instrument);
                }
            }
        }

        private void EnableInstrumentIcon(int idx, Instrument instrument)
        {
            var icon = Addressables.LoadAssetAsync<Sprite>($"InstrumentIcons[{instrument.ToResourceName()}]").WaitForCompletion();
            _instrumentIcons[idx].sprite = icon;
            _instrumentIcons[idx].enabled = true;
        }
    }
}