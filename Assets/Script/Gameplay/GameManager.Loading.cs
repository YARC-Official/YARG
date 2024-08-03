using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Cysharp.Threading.Tasks;
using UnityEngine;
using YARG.Audio;
using YARG.Core;
using YARG.Core.Audio;
using YARG.Core.Chart;
using YARG.Core.Logging;
using YARG.Core.Replays;
using YARG.Gameplay.Player;
using YARG.Menu.Navigation;
using YARG.Menu.Persistent;
using YARG.Menu.Settings;
using YARG.Playback;
using YARG.Player;
using YARG.Replays;
using YARG.Scores;
using YARG.Settings;
using YARG.Song;

namespace YARG.Gameplay
{
    public partial class GameManager
    {
        private enum LoadFailureState
        {
            None,
            Rescan,
            Error
        }

        [Header("Instrument Prefabs")]
        [SerializeField]
        private GameObject _fiveFretGuitarPrefab;
        [SerializeField]
        private GameObject _sixFretGuitarPrefab;
        [SerializeField]
        private GameObject _fourLaneDrumsPrefab;
        [SerializeField]
        private GameObject _fiveLaneDrumsPrefab;
        [SerializeField]
        private GameObject _proKeysPrefab;
        [SerializeField]
        private GameObject _proGuitarPrefab;

        private LoadFailureState _loadState;
        private string _loadFailureMessage;

        // All access to chart data must be done through this event,
        // since things are loaded asynchronously
        // Players are initialized by hand and don't go through this event
        private event Action<SongChart> _chartLoaded;

        public event Action<SongChart> ChartLoaded
        {
            add
            {
                _chartLoaded += value;

                // Invoke now if already loaded, this event is only fired once
                var chart = Chart;
                if (chart != null) value?.Invoke(chart);
            }
            remove => _chartLoaded -= value;
        }

        private event Action _songLoaded;

        public event Action SongLoaded
        {
            add
            {
                _songLoaded += value;

                // Invoke now if already loaded, this event is only fired once
                if (_mixer != null)
                {
                    value?.Invoke();
                }
            }
            remove => _songLoaded -= value;
        }

        private event Action _songStarted;

        public event Action SongStarted
        {
            add
            {
                _songStarted += value;

                // Invoke now if already loaded, this event is only fired once
                if (IsSongStarted) value?.Invoke();
            }
            remove => _songStarted -= value;
        }

        private async void Start()
        {
            // Displays the loading screen
            using var context = new LoadingContext();
            var global = GlobalVariables.Instance;

            // Disable until everything's loaded
            enabled = false;

            YargLogger.LogFormatInfo("Loading song {0} - {1}", Song.Name, Song.Artist);

            if (IsReplay)
            {
                if (!SongContainer.SongsByHash.TryGetValue(
                    GlobalVariables.State.CurrentReplay.SongChecksum, out var songs))
                {
                    ToastManager.ToastWarning("Song not present in library");
                    global.LoadScene(SceneIndex.Menu);
                    return;
                }
                Song = songs[0];

                context.SetLoadingText("Loading replay...");
                if (!LoadReplay())
                {
                    ToastManager.ToastError("Failed to load replay!");
                    global.LoadScene(SceneIndex.Menu);
                    return;
                }
                _replayController.gameObject.SetActive(true);
            }

            context.Queue(UniTask.RunOnThreadPool(LoadChart), "Loading chart...");
            context.Queue(UniTask.RunOnThreadPool(LoadAudio), "Loading audio...");
            await context.Wait();

            if (_loadState == LoadFailureState.Rescan)
            {
                ToastManager.ToastWarning("Chart requires a rescan!", () =>
                {
                    SettingsMenu.Instance.gameObject.SetActive(true);
                    SettingsMenu.Instance.SelectTabByName("SongManager");
                });

                global.LoadScene(SceneIndex.Menu);
                return;
            }

            if (_loadState == LoadFailureState.Error)
            {
                YargLogger.LogError(_loadFailureMessage);
                ToastManager.ToastError(_loadFailureMessage);

                global.LoadScene(SceneIndex.Menu);
                return;
            }

            FinalizeChart();

            // Initialize song runner
            _songRunner = new SongRunner(
                _mixer,
                GlobalVariables.State.SongSpeed,
                SettingsManager.Settings.AudioCalibration.Value,
                SettingsManager.Settings.VideoCalibration.Value,
                Song.SongOffsetSeconds);

            // Spawn players
            CreatePlayers();

            // Listen for menu inputs
            Navigator.Instance.NavigationEvent += OnNavigationEvent;

            // Debug info
            InitializeDebugGUI();
#if UNITY_EDITOR
            SetDebugEnabled(true);
#endif

            // Initialize/destroy practice mode
            if (IsPractice)
            {
                PracticeManager.DisplayPracticeMenu();
            }
            else
            {
                Destroy(PracticeManager);
            }

            // TODO: Move the offset here to SFX configuration
            // The clap SFX has 20 ms of lead-up before the actual impact happens
            BeatEventHandler.Subscribe(StarPowerClap, -0.02);

            // Log constant values
            YargLogger.LogFormatDebug("Audio calibration: {0}, video calibration: {1}, song offset: {2}",
                _songRunner.AudioCalibration, _songRunner.VideoCalibration, _songRunner.SongOffset);

            // Loaded, enable updates
            enabled = true;
            IsSongStarted = true;
            _songStarted?.Invoke();
        }

        private bool LoadReplay()
        {
            ReplayFile replayFile = null!;
            ReplayReadResult result;
            try
            {
                result = ReplayContainer.LoadReplayFile(GlobalVariables.State.CurrentReplay, out replayFile);
            }
            catch (Exception ex)
            {
                result = ReplayReadResult.Corrupted;
                YargLogger.LogException(ex, "Failed to load replay!");
            }

            if (result != ReplayReadResult.Valid)
            {
                return false;
            }

            Replay = replayFile.Replay;

            // Create YargPlayers from the replay frames
            var players = new List<YargPlayer>();
            foreach (var frame in Replay.Frames)
            {
                var yargPlayer = new YargPlayer(frame.PlayerInfo.Profile, null, false);

                yargPlayer.SetPresetsFromReplay(Replay.ReplayPresetContainer);
                yargPlayer.EngineParameterOverride = frame.EngineParameters;

                players.Add(yargPlayer);
            }

            YargPlayers = players;
            return true;
        }

        private void LoadChart()
        {
            try
            {
                Chart = Song.LoadChart();
                if (Chart != null)
                {
                    GenerateVenueTrack();
                }
                else
                {
                    _loadState = LoadFailureState.Rescan;
                }
            }
            catch (Exception ex)
            {
                _loadState = LoadFailureState.Error;
                _loadFailureMessage = "Failed to load chart!";
                YargLogger.LogException(ex);
            }
        }

        private void GenerateVenueTrack()
        {
            if (File.Exists(VenueAutoGenerationPreset.DefaultPath))
            {
                var preset = new VenueAutoGenerationPreset(VenueAutoGenerationPreset.DefaultPath);
                if (!preset.ChartHasFog(Chart)) // This is separate because we may want to add fog even if venue is authored
                {
                    Chart = preset.GenerateFogEvents(Chart);
                }

                if (Chart.VenueTrack.Lighting.Count == 0)
                {
                    Chart = preset.GenerateLightingEvents(Chart);
                }

                // TODO: add when characters and camera events are present in game
                // if (Chart.VenueTrack.Camera.Count == 0)
                // {
                //     Chart = autoGenerationPreset.GenerateCameraCutEvents(Chart);
                // }
            }
        }

        private void FinalizeChart()
        {
            BeatEventHandler = new BeatEventHandler(Chart.SyncTrack);
            _chartLoaded?.Invoke(Chart);

            double audioLength = _mixer.Length;
            double chartLength = Chart.GetEndTime();
            double endTime = Chart.GetEndEvent()?.Time ?? -1;

            // - Chart < Audio < [end] -> Audio
            // - Chart < [end] < Audio -> [end]
            // - [end] < Chart < Audio -> Audio
            // - Audio < Chart         -> Chart
            if (audioLength <= chartLength)
            {
                SongLength = chartLength;
            }
            else if (endTime <= chartLength || audioLength <= endTime)
            {
                SongLength = audioLength;
            }
            else
            {
                SongLength = endTime;
            }
            _songLoaded?.Invoke();
        }

        private void CreatePlayers()
        {
            _players = new List<BasePlayer>();

            bool vocalTrackInitialized = false;

            int index = -1;
            foreach (var player in YargPlayers)
            {
                index++;

                if (!IsReplay)
                {
                    // Reset microphone (resets channel buffers)
                    // We probably wanna do this no matter what, so put it up here
                    player.Bindings.Microphone?.Reset();
                }

                // Skip if the player is sitting out
                if (player.SittingOut)
                {
                    continue;
                }

                if (!IsReplay)
                {
                    // Don't do this if it's a replay, because the replay
                    // would've already set its own presets at this point
                    player.SetPresetsFromProfile();
                }

                var lastHighScore = ScoreContainer
                    .GetHighScoreByInstrument(Song.Hash, player.Profile.CurrentInstrument)?
                    .Score;

                if (player.Profile.GameMode != GameMode.Vocals)
                {
                    var prefab = player.Profile.GameMode switch
                    {
                        GameMode.FiveFretGuitar => _fiveFretGuitarPrefab,
                        GameMode.SixFretGuitar  => _sixFretGuitarPrefab,
                        GameMode.FourLaneDrums  => _fourLaneDrumsPrefab,
                        GameMode.FiveLaneDrums  => _fiveLaneDrumsPrefab,
                        GameMode.ProKeys        => _proKeysPrefab,
                        GameMode.ProGuitar      => _proGuitarPrefab,
                        _ => null
                    };

                    // Skip if there's no prefab for the game mode
                    if (prefab == null) continue;

                    var playerObject = Instantiate(prefab,
                        new Vector3(index * TRACK_SPACING_X, 100f, 0f), prefab.transform.rotation);

                    // Setup player
                    var trackPlayer = playerObject.GetComponent<TrackPlayer>();
                    var trackView = _trackViewManager.CreateTrackView(trackPlayer, player);
                    trackPlayer.Initialize(index, player, Chart, trackView, _mixer, lastHighScore);
                    _players.Add(trackPlayer);
                }
                else
                {
                    // Initialize the vocal track if it hasn't been already, and hide lyric bar
                    if (!vocalTrackInitialized)
                    {
                        VocalTrack.gameObject.SetActive(true);
                        _trackViewManager.CreateVocalTrackView();

                        // Since all players have to select the same vocals
                        // type (solo/harmony) this works no problem.
                        var chart = player.Profile.CurrentInstrument == Instrument.Vocals
                            ? Chart.Vocals
                            : Chart.Harmony;
                        VocalTrack.Initialize(chart, player);

                        _lyricBar.SetActive(false);
                        vocalTrackInitialized = true;
                    }

                    // Create the player on the vocal track

                    var vocalsPlayer = VocalTrack.CreatePlayer();

                    var playerHud = _trackViewManager.CreateVocalsPlayerHUD();

                    var percussionTrack = VocalTrack.CreatePercussionTrack();
                    vocalsPlayer.Initialize(index, player, Chart, playerHud, percussionTrack, lastHighScore);

                    _players.Add(vocalsPlayer);
                }

                // Add (or increase total of) the stem state
                var stem = player.Profile.CurrentInstrument.ToSongStem();
                if (stem == SongStem.Bass && !_stemStates.ContainsKey(SongStem.Bass))
                {
                    stem = SongStem.Rhythm;
                }

                if (stem != _backgroundStem && _stemStates.TryGetValue(stem, out var state))
                {
                    ++state.Total;
                    ++state.Audible;
                }
                else if (_stemStates.TryGetValue(_backgroundStem, out state))
                {
                    // Ensures the stem will still play at a minimum of 50%, even if all players mute
                    state.Total += 2;
                    state.Audible += 2;
                }
            }

            // Make sure to set up all of the HUD positions
            _trackViewManager.SetAllHUDPositions();
        }
    }
}
