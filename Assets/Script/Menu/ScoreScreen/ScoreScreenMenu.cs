using System;
using System.Linq;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Core;
using YARG.Core.Audio;
using YARG.Core.Engine.Drums;
using YARG.Core.Engine.Guitar;
using YARG.Core.Engine.Keys;
using YARG.Core.Engine.Vocals;
using YARG.Core.Input;
using YARG.Core.Logging;
using YARG.Core.Replays;
using YARG.Core.Replays.Analyzer;
using YARG.Core.Song;
using YARG.Localization;
using YARG.Menu.MusicLibrary;
using YARG.Menu.Navigation;
using YARG.Menu.Persistent;
using YARG.Scores;
using YARG.Song;
using YARG.Playlists;
using YARG.Helpers.Extensions;
using YARG.Core.Engine;
using YARG.Playback;
using YARG.Settings;

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
        private TextMeshProUGUI _bandScoreNotSavedMessage;
        [SerializeField]
        private ScrollRect _cardScrollRect;
        [SerializeField]
        private float _horizontalScrollRate = 30f;
        [SerializeField]
        private float _verticalScrollRate = 15f;

        [Space]
        [SerializeField]
        private GuitarScoreCard _guitarCardPrefab;
        [SerializeField]
        private DrumsScoreCard _drumsCardPrefab;
        [SerializeField]
        private VocalsScoreCard _vocalsCardPrefab;
        [SerializeField]
        private ProKeysScoreCard _proKeysCardPrefab;
        [SerializeField]
        private ProKeysScoreCard _fiveLaneKeysCardPrefab;

        private bool _analyzingReplay;

        private bool _restartingSong;

        private readonly List<IScoreCard<BaseStats>> _scoreCards = new();

        private void OnEnable()
        {
            var song = GlobalVariables.State.CurrentSong;

            SetNavigationScheme();

            if (GlobalVariables.State.ScoreScreenStats is null)
            {
                YargLogger.LogError("Score screen stats was null!");
                return;
            }

            var scoreScreenStats = GlobalVariables.State.ScoreScreenStats.Value;

#if UNITY_EDITOR || YARG_NIGHTLY_BUILD || YARG_TEST_BUILD
            // Do analysis of replay before showing any score data
            // This will make it so that if the analysis takes a while the screen is blank
            // (kinda like a loading screen)
            try
            {
                if (!AnalyzeReplay(song, scoreScreenStats.ReplayInfo))
                {
                    DialogManager.Instance.ShowMessage("Inconsistent Replay Results!",
                        "The replay analysis for this run produced inconsistent results to the actual gameplay.\n" +
                        "Please report this issue to the YARG developers on GitHub or Discord.\n\n" +
                        $"Chart Hash: {song.Hash}");
                }
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, $"Failed to analyze replay! Song hash: {song.Hash}");
                DialogManager.Instance.ShowMessage("Failed To Analyze Replay!",
                    "The replay analysis for this run resulted in an unexpected error.\n" +
                    "Please report this issue to the YARG developers on GitHub or Discord.\n\n" +
                    $"Chart Hash: {song.Hash}");
            }
#endif

            // Play audience chatter
            if (SettingsManager.Settings.UseCrowdFx.Value == CrowdFxMode.Enabled)
            {
                GlobalAudioHandler.PlaySoundEffect(SfxSample.Chatter, 1.0);
            }

            // Set text
            _songTitle.text = song.Name;
            _artistName.text = song.Artist;
            _bandScoreNotSavedMessage.gameObject.SetActive(
                !ScoreContainer.IsBandScoreValid(PersistentState.Default.SongSpeed));

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

            //set restarting state
            _restartingSong = false;
        }

        private void OnDisable()
        {
            MusicLibraryMenu.CurrentlyPlaying = GlobalVariables.State.CurrentSong;
            if (!GlobalVariables.State.PlayingAShow && !_restartingSong)
            {
                GlobalVariables.State = PersistentState.Default;
            }

            if (SettingsManager.Settings.UseCrowdFx.Value == CrowdFxMode.Enabled)
            {
                GlobalAudioHandler.StopSoundEffect(SfxSample.Chatter, 1.0);
            }

            Navigator.Instance.PopScheme();
        }

        private void CreateScoreCards(ScoreScreenStats scoreScreenStats)
        {
            int fcCount = 0;
            int highScoreCount = 0;

            foreach (var score in scoreScreenStats.PlayerScores)
            {
                // Bots don't get vox
                if (!score.Player.Profile.IsBot)
                {
                    // We intentionally don't count both high score and full combo
                    if (score.Stats.IsFullCombo)
                    {
                        fcCount++;
                    }
                    else if (score.IsHighScore)
                    {
                        highScoreCount++;
                    }
                }

                IScoreCard<BaseStats> card = null;

                switch (score.Player.Profile.GameMode)
                {
                    case GameMode.FiveFretGuitar:
                    {
                        card = Instantiate(_guitarCardPrefab, _cardContainer);
                        ((ScoreCard<GuitarStats>)card).Initialize(score.IsHighScore, score.Player, score.Stats as GuitarStats);
                        break;
                    }
                    case GameMode.FourLaneDrums:
                    case GameMode.FiveLaneDrums:
                    case GameMode.EliteDrums:
                    {
                        card = Instantiate(_drumsCardPrefab, _cardContainer);
                        ((ScoreCard<DrumsStats>)card).Initialize(score.IsHighScore, score.Player, score.Stats as DrumsStats);
                        break;
                    }
                    case GameMode.Vocals:
                    {
                        card = Instantiate(_vocalsCardPrefab, _cardContainer);
                        ((ScoreCard<VocalsStats>)card).Initialize(score.IsHighScore, score.Player, score.Stats as VocalsStats);
                        break;
                    }
                    case GameMode.ProKeys:
                    {
                        if (score.Player.Profile.CurrentInstrument is Instrument.ProKeys)
                        {
                            card = Instantiate(_proKeysCardPrefab, _cardContainer);
                        }
                        else
                        {
                            card = Instantiate(_fiveLaneKeysCardPrefab, _cardContainer);
                        }
                        ((ScoreCard<KeysStats>) card).Initialize(score.IsHighScore, score.Player,
                            score.Stats as KeysStats);
                        break;
                    }
                }

                Debug.Assert(card != null, $"ScoreCard not initialized for GameMode: {score.Player.Profile.GameMode}");
                card.SetCardContents();
                _scoreCards.Add(card);
            }

            // Mark that the music library should refresh when next opened
            if (GlobalVariables.State.ScoreScreenStats.Value.PlayerScores.Any(e => !e.Player.Profile.IsBot))
            {
                MusicLibraryMenu.NeedsReload();
            }

            // Make sure to update the canvases since we *just* added the score cards
            Canvas.ForceUpdateCanvases();

            // If the scroll bar is active, make it all the way to the left
            InitializeScrollRect();

            // As a final bonus, play the appropriate full combo/high score vox samples
            PlayScoreVox(fcCount, highScoreCount);
        }

        private async void InitializeScrollRect()
        {
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
            _cardScrollRect.horizontalNormalizedPosition = 0f;
        }

        private static void PlayScoreVox(int fcCount, int highScoreCount)
        {
            if (fcCount > 0)
            {
                GlobalAudioHandler.PlayVoxSample(VoxSample.FullCombo);
                YargLogger.LogInfo("Playing full combo vox sample");
            }

            if (fcCount > 1)
            {
                YargLogger.LogDebug($"Playing full combo vox sample for {fcCount} times");
                switch (fcCount)
                {
                    case 2:
                        GlobalAudioHandler.PlayVoxSample(VoxSample.Times2);
                        break;
                    case 3:
                        GlobalAudioHandler.PlayVoxSample(VoxSample.Times3);
                        break;
                    case 4:
                        GlobalAudioHandler.PlayVoxSample(VoxSample.Times4);
                        break;
                    case 5:
                        GlobalAudioHandler.PlayVoxSample(VoxSample.Times5);
                        break;
                    case 6:
                        GlobalAudioHandler.PlayVoxSample(VoxSample.Times6);
                        break;
                    case > 6:
                        GlobalAudioHandler.PlayVoxSample(VoxSample.TimesMany);
                        break;
                }
            }

            if (highScoreCount > 0)
            {
                GlobalAudioHandler.PlayVoxSample(VoxSample.HighScore);
                YargLogger.LogInfo("Playing high score vox sample");
            }

            if (highScoreCount > 1)
            {
                switch (highScoreCount)
                {
                    case 2:
                        GlobalAudioHandler.PlayVoxSample(VoxSample.Times2);
                        break;
                    case 3:
                        GlobalAudioHandler.PlayVoxSample(VoxSample.Times3);
                        break;
                    case 4:
                        GlobalAudioHandler.PlayVoxSample(VoxSample.Times4);
                        break;
                    case 5:
                        GlobalAudioHandler.PlayVoxSample(VoxSample.Times5);
                        break;
                    case 6:
                        GlobalAudioHandler.PlayVoxSample(VoxSample.Times6);
                        break;
                    case > 6:
                        GlobalAudioHandler.PlayVoxSample(VoxSample.TimesMany);
                        break;
                }
            }
        }

#nullable enable
        private bool AnalyzeReplay(SongEntry songEntry, ReplayInfo? replayEntry)
#nullable disable
        {
            _analyzingReplay = true;

            var chart = songEntry.LoadChart();
            if (chart == null)
            {
                YargLogger.LogError("Chart did not load");
                _analyzingReplay = false;
                return true;
            }

            if (GlobalVariables.State.ScoreScreenStats.Value.PlayerScores.All(e => e.Player.Profile.IsBot))
            {
                YargLogger.LogInfo("No human players in ReplayEntry.");
                _analyzingReplay = false;
                return true;
            }

            if (replayEntry == null)
            {
                YargLogger.LogError("ReplayEntry is null");
                _analyzingReplay = false;
                return true;
            }

            var replayOptions = new ReplayReadOptions
            {
                KeepFrameTimes = GlobalVariables.VerboseReplays
            };
            var (result, data) = ReplayIO.TryLoadData(replayEntry, replayOptions);
            if (result != ReplayReadResult.Valid)
            {
                YargLogger.LogFormatError("Replay did not load. {0}", result);
                _analyzingReplay = false;
                return true;
            }

            var results = ReplayAnalyzer.AnalyzeReplay(chart, replayEntry, data);
            bool allPass = true;

            for (int i = 0; i < results.Length; i++)
            {
                var analysisResult = results[i];

                // Always print the stats in debug mode
#if UNITY_EDITOR || YARG_TEST_BUILD
                YargLogger.LogFormatInfo("({0}, {1}/{2}) Verification Result: {3}. Stats:\n{4}",
                    data.Frames[i].Profile.Name, data.Frames[i].Profile.CurrentInstrument,
                    data.Frames[i].Profile.CurrentDifficulty, item4: analysisResult.Passed ? "Passed" : "Failed",
                    item5: analysisResult.StatLog);
#endif

                if (!analysisResult.Passed)
                {
#if !(UNITY_EDITOR || YARG_TEST_BUILD)
                    YargLogger.LogFormatWarning("({0}, {1}/{2}) FAILED verification. Stats:\n{3}",
                        data.Frames[i].Profile.Name, data.Frames[i].Profile.CurrentInstrument,
                        data.Frames[i].Profile.CurrentDifficulty, item4: analysisResult.StatLog);
#endif
                    _analyzingReplay = false;
                    allPass = false;
                }
            }

            _analyzingReplay = false;
            return allPass;
        }

        private NavigationScheme.Entry _continueButtonEntry;
        private NavigationScheme.Entry _endEarlyButtonEntry;
        private NavigationScheme.Entry _restartButtonEntry;
        private NavigationScheme.Entry _removeFavoriteButtonEntry;
        private NavigationScheme.Entry _addFavoriteButtonEntry;
        private NavigationScheme.Entry _scrollLeftEntry;
        private NavigationScheme.Entry _scrollRightEntry;
        private NavigationScheme.Entry _scrollUpEntry;
        private NavigationScheme.Entry _scrollDownEntry;

        private void SetNavigationScheme()
        {
            var song = GlobalVariables.State.CurrentSong;

            _continueButtonEntry = new NavigationScheme.Entry(MenuAction.Green, "Menu.Common.Continue", () =>
                {
                    if (!_analyzingReplay)
                    {
                        GlobalVariables.State.ShowIndex++;
                        if (GlobalVariables.State.PlayingAShow &&
                            GlobalVariables.State.ShowIndex < GlobalVariables.State.ShowSongs.Count)
                        {
                            // Reset CurrentSong and launch back into the Gameplay scene
                            GlobalVariables.State.CurrentSong =
                                GlobalVariables.State.ShowSongs[GlobalVariables.State.ShowIndex];
                            GlobalVariables.Instance.LoadScene(SceneIndex.Gameplay);
                        }
                        else
                        {
                            GlobalVariables.State.PlayingAShow = false;
                            GlobalVariables.Instance.LoadScene(SceneIndex.Menu);
                        }
                    }
                });

            _endEarlyButtonEntry = new NavigationScheme.Entry(MenuAction.Red, "Menu.ScoreScreen.EndSetlistEarly", () =>
            {
                GlobalVariables.State.PlayingAShow = false;
                GlobalVariables.Instance.LoadScene(SceneIndex.Menu);
            });

            _restartButtonEntry = new NavigationScheme.Entry(MenuAction.Yellow, "Menu.ScoreScreen.RestartSong", () =>
            {
                _restartingSong = true;
                GlobalVariables.Instance.LoadScene(SceneIndex.Gameplay);
            });

            _addFavoriteButtonEntry = new NavigationScheme.Entry(MenuAction.Blue, "Menu.MusicLibrary.Popup.Item.AddToFavorites", () =>
                {
                    YargLogger.LogInfo("added favorite");
                    PlaylistContainer.FavoritesPlaylist.AddSong(song);
                    UpdateNavigationScheme(true);
                });

            _removeFavoriteButtonEntry = new NavigationScheme.Entry(MenuAction.Blue, "Menu.MusicLibrary.Popup.Item.RemoveFromFavorites", () =>
                {
                    YargLogger.LogInfo("removed favorite");
                    PlaylistContainer.FavoritesPlaylist.RemoveSong(song);
                    UpdateNavigationScheme(true);
                });

            _scrollLeftEntry = new NavigationScheme.Entry(MenuAction.Left, "Menu.Common.Scroll", context =>
                {
                    _cardScrollRect.MoveHorizontalInUnits(-1 * _horizontalScrollRate);
                });

            _scrollRightEntry = new NavigationScheme.Entry(MenuAction.Right, "Menu.Common.Scroll", context =>
                {
                    _cardScrollRect.MoveHorizontalInUnits(_horizontalScrollRate);
                });

            _scrollUpEntry = new NavigationScheme.Entry(MenuAction.Up, "Menu.Common.Scroll", context =>
                {
                    ScrollScoreCard(context.Player, _verticalScrollRate);
                });

            _scrollDownEntry = new NavigationScheme.Entry(MenuAction.Down, "Menu.Common.Scroll", context =>
                {
                    ScrollScoreCard(context.Player, -1 * _verticalScrollRate);
                });

            UpdateNavigationScheme();
        }

        private void ScrollScoreCard(Player.YargPlayer player, float delta)
        {
            var card = _scoreCards.FirstOrDefault(card => card.Player == player);
            card?.ScrollStats(delta);
        }

        private void UpdateNavigationScheme(bool reset = false)
        {
            if (reset)
            {
                Navigator.Instance.PopScheme();
            }

            List<NavigationScheme.Entry> buttons = new()
            {
                _continueButtonEntry,
                _restartButtonEntry
            };

            var song = GlobalVariables.State.CurrentSong;
            var isFavorited = PlaylistContainer.FavoritesPlaylist.ContainsSong(song);

            if (isFavorited)
            {
                buttons.Add(_removeFavoriteButtonEntry);
            }
            else
            {
                buttons.Add(_addFavoriteButtonEntry);
            }

            if (GlobalVariables.State.PlayingAShow &&
                GlobalVariables.State.ShowIndex + 1 < GlobalVariables.State.ShowSongs.Count)
            {
                buttons.Insert(1, _endEarlyButtonEntry);
            }

            buttons.Add(_scrollLeftEntry);
            buttons.Add(_scrollRightEntry);
            buttons.Add(_scrollUpEntry);
            buttons.Add(_scrollDownEntry);
            Navigator.Instance.PushScheme(new(buttons, true));
        }
    }
}
