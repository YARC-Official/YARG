using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Cysharp.Threading.Tasks;
using UnityEngine;
using YARG.Core;
using YARG.Core.Audio;
using YARG.Core.Chart;
using YARG.Core.Replays;
using YARG.Gameplay.Player;
using YARG.Menu.Navigation;
using YARG.Menu.Persistent;
using YARG.Playback;
using YARG.Player;
using YARG.Replays;
using YARG.Scores;
using YARG.Settings;

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
                if (GlobalVariables.AudioManager.IsAudioLoaded) value?.Invoke();
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

        // "The Unity message 'Start' has an incorrect signature."
        [SuppressMessage("Type Safety", "UNT0006", Justification = "UniTaskVoid is a compatible return type.")]
        private async UniTaskVoid Start()
        {
            // Disable until everything's loaded
            enabled = false;

            // Load song
            if (IsReplay)
            {
                LoadingManager.Instance.Queue(LoadReplay, "Loading replay...");
                _replayController.gameObject.SetActive(true);
            }

            LoadingManager.Instance.Queue(LoadChart, "Loading chart...");
            LoadingManager.Instance.Queue(LoadAudio, "Loading audio...");
            await LoadingManager.Instance.StartLoad();

            if (_loadState == LoadFailureState.Rescan)
            {
                ToastManager.ToastWarning("Chart requires a rescan!");

                GlobalVariables.Instance.LoadScene(SceneIndex.Menu);
                return;
            }

            if (_loadState == LoadFailureState.Error)
            {
                Debug.LogError(_loadFailureMessage);
                ToastManager.ToastError(_loadFailureMessage);

                GlobalVariables.Instance.LoadScene(SceneIndex.Menu);
                return;
            }

            // Initialize song runner
            _songRunner = new SongRunner(
                GlobalVariables.Instance.SongSpeed,
                SettingsManager.Settings.AudioCalibration.Value,
                SettingsManager.Settings.VideoCalibration.Value,
                Song.SongOffsetSeconds);

            // Spawn players
            CreatePlayers();

            // Listen for menu inputs
            Navigator.Instance.NavigationEvent += OnNavigationEvent;

            // Initialize/destroy practice mode
            if (IsPractice)
            {
                PracticeManager.DisplayPracticeMenu();
            }
            else
            {
#if UNITY_EDITOR
                _isShowDebugText = true;
#endif

                // Show debug info
                _debugText.gameObject.SetActive(_isShowDebugText);

                Destroy(PracticeManager);
            }

#if UNITY_EDITOR
            // Log constant values
            Debug.Log($"Audio calibration: {_songRunner.AudioCalibration}, video calibration: {_songRunner.VideoCalibration}, song offset: {_songRunner.SongOffset}");
#endif

            // Loaded, enable updates
            enabled = true;
            IsSongStarted = true;
            _songStarted?.Invoke();
        }

        private async UniTask LoadReplay()
        {
            ReplayFile replayFile = null;
            ReplayReadResult result;
            try
            {
                result = await UniTask.RunOnThreadPool(() => ReplayContainer.LoadReplayFile(
                    GlobalVariables.Instance.CurrentReplay, out replayFile));
            }
            catch (Exception ex)
            {
                _loadState = LoadFailureState.Error;
                _loadFailureMessage = "Failed to load replay!";
                Debug.LogException(ex, this);
                return;
            }

            Song = GlobalVariables.Instance.SongContainer.SongsByHash[
                GlobalVariables.Instance.CurrentReplay.SongChecksum][0];

            if (Song is null || result != ReplayReadResult.Valid)
            {
                _loadState = LoadFailureState.Error;
                _loadFailureMessage = "Failed to load replay!";
                return;
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

            _yargPlayers = players;
        }

        private async UniTask LoadChart()
        {
            await UniTask.RunOnThreadPool(() =>
            {
                // Load chart

                try
                {
                    Chart = Song.LoadChart();
                }
                catch (Exception ex)
                {
                    _loadState = LoadFailureState.Error;
                    _loadFailureMessage = "Failed to load chart!";
                    Debug.LogException(ex, this);
                    return;
                }

                if (Chart is null)
                {
                    _loadState = LoadFailureState.Rescan;
                    return;
                }

                // Ensure sync track is present
                var syncTrack = Chart.SyncTrack;
                if (syncTrack.Beatlines is null or { Count: < 1 })
                {
                    Chart.SyncTrack.GenerateBeatlines(Chart.GetLastTick());
                }

                // Set length of the final section
                if (Chart.Sections.Count > 0)
                {
                    uint lastTick = Chart.GetLastTick();
                    Chart.Sections[^1].TickLength = lastTick;
                    Chart.Sections[^1].TimeLength = Chart.SyncTrack.TickToTime(lastTick);
                }

                // Autogenerate venue stuff
                if (File.Exists(VenueAutoGenerationPreset.DefaultPath))
                {
                    var preset = new VenueAutoGenerationPreset(VenueAutoGenerationPreset.DefaultPath);
                    if (Chart.VenueTrack.Lighting.Count == 0)
                    {
                        Debug.Log("Auto-generating venue lighting...");
                        Chart = preset.GenerateLightingEvents(Chart);
                    }

                    // TODO: add when characters and camera events are present in game
                    // if (Chart.VenueTrack.Camera.Count == 0)
                    // {
                    //     Chart = autoGenerationPreset.GenerateCameraCutEvents(Chart);
                    // }
                }
            });

            if (_loadState != LoadFailureState.None) return;

            BeatEventHandler = new(Chart.SyncTrack);
            _chartLoaded?.Invoke(Chart);
        }

        private void CreatePlayers()
        {
            _players = new List<BasePlayer>();

            bool vocalTrackInitialized = false;

            int index = -1;
            foreach (var player in _yargPlayers)
            {
                index++;

                // Skip if the player is sitting out
                if (player.SittingOut)
                {
                    continue;
                }

                // Don't do this if it's a replay, because the replay
                // would've already set its own presets at this point
                if (!IsReplay)
                {
                    player.SetPresetsFromProfile();
                }

                if (player.Profile.GameMode != GameMode.Vocals)
                {
                    var prefab = player.Profile.GameMode switch
                    {
                        GameMode.FiveFretGuitar => _fiveFretGuitarPrefab,
                        GameMode.SixFretGuitar  => _sixFretGuitarPrefab,
                        GameMode.FourLaneDrums  => _fourLaneDrumsPrefab,
                        GameMode.FiveLaneDrums  => _fiveLaneDrumsPrefab,
                        GameMode.ProGuitar      => _proGuitarPrefab,

                        _ => null
                    };

                    // Skip if there's no prefab for the game mode
                    if (prefab == null) continue;

                    var playerObject = Instantiate(prefab,
                        new Vector3(index * 100f, 100f, 0f), prefab.transform.rotation);

                    // Setup player
                    var trackPlayer = playerObject.GetComponent<TrackPlayer>();
                    var trackView = _trackViewManager.CreateTrackView(trackPlayer, player);
                    var currentHighScore = ScoreContainer.GetHighScoreByInstrument(Song.Hash, player.Profile.CurrentInstrument)?.Score;
                    trackPlayer.Initialize(index, player, Chart, trackView, currentHighScore);
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
                        VocalTrack.Initialize(chart);

                        _lyricBar.SetActive(false);
                        vocalTrackInitialized = true;
                    }

                    // Create the player on the vocal track
                    var vocalsPlayer = VocalTrack.CreatePlayer();
                    var playerHud = _trackViewManager.CreateVocalsPlayerHUD();
                    vocalsPlayer.Initialize(index, player, Chart, playerHud);
                    _players.Add(vocalsPlayer);
                }

                // Add (or increase total of) the stem state
                var stem = player.Profile.CurrentInstrument.ToSongStem();
                if (_stemStates.TryGetValue(stem, out var state))
                {
                    state.Total++;
                }
                else
                {
                    _stemStates.Add(stem, new StemState
                    {
                        Total = 1
                    });
                }
            }

            // Make sure to set up all of the HUD positions
            _trackViewManager.SetAllHUDPositions();
        }
    }
}