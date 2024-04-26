using System.Diagnostics.CodeAnalysis;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Core;
using YARG.Core.Engine.Drums;
using YARG.Core.Engine.Guitar;
using YARG.Core.Engine.Vocals;
using YARG.Core.Input;
using YARG.Core.Logging;
using YARG.Core.Song;
using YARG.Helpers;
using YARG.Menu.Navigation;
using YARG.Song;

namespace YARG.Menu.ScoreScreen
{
    public class ScoreScreenMenu : MonoBehaviour
    {
        [SerializeField]
        private Transform _cardContainer;
        [SerializeField]
        private Image _sourceIcon;
        [SerializeField]
        private TextMeshProUGUI _songTitle;
        [SerializeField]
        private TextMeshProUGUI _artistName;
        [SerializeField]
        private StarView _bandStarView;
        [SerializeField]
        private TextMeshProUGUI _bandScore;
        [SerializeField]
        private Scrollbar _horizontalScrollBar;

        [Space]
        [SerializeField]
        private GuitarScoreCard _guitarCardPrefab;
        [SerializeField]
        private DrumsScoreCard _drumsCardPrefab;
        [SerializeField]
        private VocalsScoreCard _vocalsCardPrefab;

        private void OnEnable()
        {
            // Set navigation scheme
            Navigator.Instance.PushScheme(new NavigationScheme(new()
            {
                new NavigationScheme.Entry(MenuAction.Green, "Continue", () =>
                {
                    GlobalVariables.Instance.LoadScene(SceneIndex.Menu);
                })
            }, true));

            if (GlobalVariables.State.ScoreScreenStats is null)
            {
                YargLogger.LogError("Score screen stats was null!");
                return;
            }

            var song = GlobalVariables.State.CurrentSong;
            var scoreScreenStats = GlobalVariables.State.ScoreScreenStats.Value;

            // Set text
            _songTitle.text = song.Name;
            _artistName.text = song.Artist;

            // Set speed text (if not at 100% speed)
            if (!Mathf.Approximately(GlobalVariables.State.SongSpeed, 1f))
            {
                var speed = GlobalVariables.State.SongSpeed.ToString("P0", LocaleHelper.PercentFormat);

                _songTitle.text += $" ({speed})";
            }

            // Set the band score and stars
            _bandStarView.SetStars(scoreScreenStats.BandStars);
            _bandScore.text = scoreScreenStats.BandScore.ToString("N0");

            // Put the scores in!
            CreateScoreCards(scoreScreenStats);

            // Set the icon. This is async, so we have to do it last so everything loads in.
            _sourceIcon.sprite = SongSources.SourceToIcon(song.Source);
        }

        private void OnDisable()
        {
            GlobalVariables.State = PersistentState.Default;

            Navigator.Instance.PopScheme();
        }

        private void CreateScoreCards(ScoreScreenStats scoreScreenStats)
        {
            foreach (var score in scoreScreenStats.PlayerScores)
            {
                switch (score.Player.Profile.CurrentInstrument.ToGameMode())
                {
                    case GameMode.FiveFretGuitar:
                    {
                        var card = Instantiate(_guitarCardPrefab, _cardContainer);
                        card.Initialize(score.IsHighScore, score.Player, score.Stats as GuitarStats);
                        card.SetCardContents();
                        break;
                    }
                    case GameMode.FourLaneDrums:
                    case GameMode.FiveLaneDrums:
                    {
                        var card = Instantiate(_drumsCardPrefab, _cardContainer);
                        card.Initialize(score.IsHighScore, score.Player, score.Stats as DrumsStats);
                        card.SetCardContents();
                        break;
                    }
                    case GameMode.Vocals:
                    {
                        var card = Instantiate(_vocalsCardPrefab, _cardContainer);
                        card.Initialize(score.IsHighScore, score.Player, score.Stats as VocalsStats);
                        card.SetCardContents();
                        break;
                    }
                }
            }

            // Make sure to update the canvases since we *just* added the score cards
            Canvas.ForceUpdateCanvases();

            // If the scroll bar is active, make it all the way to the left
            if (_horizontalScrollBar.gameObject.activeSelf)
            {
                _horizontalScrollBar.value = 0f;
            }
        }
    }
}
