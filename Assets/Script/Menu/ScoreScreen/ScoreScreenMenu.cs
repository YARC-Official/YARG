using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Core;
using YARG.Core.Engine.Drums;
using YARG.Core.Engine.Guitar;
using YARG.Core.Engine.ProKeys;
using YARG.Core.Engine.Vocals;
using YARG.Core.Input;
using YARG.Core.Logging;
using YARG.Core.Replays;
using YARG.Core.Replays.Analyzer;
using YARG.Core.Song;
using YARG.Localization;
using YARG.Menu.Navigation;
using YARG.Menu.Persistent;
using YARG.Replays;
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
        [SerializeField]
        private ProKeysScoreCard _proKeysCardPrefab;

        private bool _analyzingReplay;

        private void OnEnable()
        {
            // Set navigation scheme
            Navigator.Instance.PushScheme(new NavigationScheme(new()
            {
                new NavigationScheme.Entry(MenuAction.Green, "Menu.Common.Continue", () =>
                {
                    if (!_analyzingReplay)
                    {
                        GlobalVariables.Instance.LoadScene(SceneIndex.Menu);
                    }
                })
            }, true));

            if (GlobalVariables.State.ScoreScreenStats is null)
            {
                YargLogger.LogError("Score screen stats was null!");
                return;
            }

            var song = GlobalVariables.State.CurrentSong;
            var scoreScreenStats = GlobalVariables.State.ScoreScreenStats.Value;

            // Do analysis of replay before showing any score data
            // This will make it so that if the analysis takes a while the screen is blank
            // (kinda like a loading screen)
            if (!AnalyzeReplay(song, scoreScreenStats.ReplayEntry))
            {
                DialogManager.Instance.ShowMessage("Inconsistent Replay Results!",
                    "The replay analysis for this run produced inconsistent results to the actual gameplay.\n" +
                    "Please report this issue to the YARG developers on GitHub or Discord.\n\n" +
                    $"Chart Hash: {song.Hash.ToString()}");
            }

            // Set text
            _songTitle.text = song.Name;
            _artistName.text = song.Artist;

            // Set speed text (if not at 100% speed)
            if (!Mathf.Approximately(GlobalVariables.State.SongSpeed, 1f))
            {
                var speed = Localize.Percent(GlobalVariables.State.SongSpeed);

                _songTitle.text += $" ({speed})";
            }

            // Set the band score and stars
            _bandStarView.SetStars(scoreScreenStats.BandStars);
            _bandScore.text = scoreScreenStats.BandScore.ToString("N0");

            // Put the scores in!
            CreateScoreCards(scoreScreenStats);

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
                    case GameMode.ProKeys:
                    {
                        var card = Instantiate(_proKeysCardPrefab, _cardContainer);
                        card.Initialize(score.IsHighScore, score.Player, score.Stats as ProKeysStats);
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

        private bool AnalyzeReplay(SongEntry songEntry, ReplayEntry replayEntry)
        {
            _analyzingReplay = true;

            var chart = songEntry.LoadChart();

            if (chart is null)
            {
                YargLogger.LogError("Chart did not load");
                _analyzingReplay = false;
                return true;
            }

            if (replayEntry is null)
            {
                YargLogger.LogError("ReplayEntry is null");
                _analyzingReplay = false;
                return true;
            }

            var replayReadResult = ReplayIO.ReadReplay(replayEntry.ReplayPath, out var replayFile);
            if (replayReadResult != ReplayReadResult.Valid)
            {
                YargLogger.LogFormatError("Replay did not load. {0}", replayReadResult);
                _analyzingReplay = false;
                return true;
            }

            var replay = replayFile!.Replay;

            var results = ReplayAnalyzer.AnalyzeReplay(chart, replay);

            for(int i = 0; i < results.Length; i++)
            {
                var analysisResult = results[i];

                if (!analysisResult.Passed)
                {
                    _analyzingReplay = false;
                    return false;
                }
            }

            _analyzingReplay = false;
            return true;
        }
    }
}
