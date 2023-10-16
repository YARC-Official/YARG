using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using YARG.Audio;
using YARG.Core;
using YARG.Core.Chart;
using YARG.Core.Input;
using YARG.Core.Replays;
using YARG.Core.Song;
using YARG.Gameplay.HUD;
using YARG.Gameplay.Player;
using YARG.Gameplay.Visuals;
using YARG.Helpers.Extensions;
using YARG.Integration;
using YARG.Menu.Navigation;
using YARG.Menu.Persistent;
using YARG.Menu.ScoreScreen;
using YARG.Playback;
using YARG.Player;
using YARG.Replays;
using YARG.Settings;

namespace YARG.Gameplay
{
    [DefaultExecutionOrder(-1)]
    public partial class GameManager : MonoBehaviour
    {
        public const double SONG_START_DELAY = SongRunner.SONG_START_DELAY;

        [Header("References")]
        [SerializeField]
        private TrackViewManager _trackViewManager;

        [SerializeField]
        private ReplayController _replayController;

        [SerializeField]
        private PauseMenuManager _pauseMenu;

        [SerializeField]
        private GameObject _lyricBar;

        [field: SerializeField]
        public VocalTrack VocalTrack { get; private set; }

        [SerializeField]
        private TextMeshProUGUI _debugText;

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

        private IReadOnlyList<YargPlayer> _yargPlayers;
        private List<BasePlayer>          _players;

        public bool IsSongStarted { get; private set; } = false;

        private enum LoadFailureState
        {
            None,
            Rescan,
            Error
        }

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

        private SongRunner _songRunner;

        public BeatEventHandler BeatEventHandler { get; private set; }

        public PracticeManager  PracticeManager  { get; private set; }
        public BackgroundManager BackgroundManager { get; private set; }

        public SongMetadata Song  { get; private set; }
        public SongChart    Chart { get; private set; }

        // For clarity, try to avoid using these properties inside GameManager itself
        // These are just to expose properties from the song runner to the outside
        /// <inheritdoc cref="SongRunner.SongTime"/>
        public double SongTime => _songRunner.SongTime;

        /// <inheritdoc cref="SongRunner.RealSongTime"/>
        public double RealSongTime => _songRunner.RealSongTime;

        /// <inheritdoc cref="SongRunner.InputTime"/>
        public double InputTime => _songRunner.InputTime;

        /// <inheritdoc cref="SongRunner.RealInputTime"/>
        public double RealInputTime => _songRunner.RealInputTime;

        /// <inheritdoc cref="SongRunner.SelectedSongSpeed"/>
        public float SelectedSongSpeed => _songRunner.SelectedSongSpeed;

        /// <inheritdoc cref="SongRunner.Paused"/>
        public bool Paused => _songRunner.Paused;

        public double SongLength { get; private set; }

        public bool IsReplay   { get; private set; }
        public bool IsPractice { get; private set; }

        public int    BandScore { get; private set; }
        public int    BandCombo { get; private set; }
        public double BandStars { get; private set; }

        public Replay Replay { get; private set; }

        public IReadOnlyList<BasePlayer> Players => _players;

        private bool _isShowDebugText;
        private bool _isReplaySaved;

        private void Awake()
        {
            // Set references
            PracticeManager = GetComponent<PracticeManager>();
            BackgroundManager = GetComponent<BackgroundManager>();

            _yargPlayers = PlayerContainer.Players;

            Song = GlobalVariables.Instance.CurrentSong;
            IsReplay = GlobalVariables.Instance.IsReplay;
            IsPractice = GlobalVariables.Instance.IsPractice && !IsReplay;

            Navigator.Instance.PopAllSchemes();
            GameStateFetcher.SetSongMetadata(Song);

            if (Song is null)
            {
                Debug.LogError("Null song set when loading gameplay!");
                GlobalVariables.Instance.LoadScene(SceneIndex.Menu);
            }

            // Hide vocals track (will be shown when players are initialized
            VocalTrack.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            Navigator.Instance.NavigationEvent -= OnNavigationEvent;
            GlobalVariables.AudioManager.SongEnd -= OnAudioEnd;
            _songRunner.Dispose();

            // Reset the time scale back, as it would be 0 at this point (because of pausing)
            Time.timeScale = 1f;
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

            // Spawn players
            CreatePlayers();

            // Initialize song runner
            float songSpeed = GlobalVariables.Instance.SongSpeed;
            double videoCalibration = -SettingsManager.Settings.VideoCalibration.Data / 1000.0;
            double audioCalibration = (-SettingsManager.Settings.AudioCalibration.Data / 1000.0) - videoCalibration;
            double songOffset = -Song.SongOffsetSeconds;
            _songRunner = new SongRunner(songSpeed, videoCalibration, audioCalibration, songOffset);

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
            Debug.Log($"Audio calibration: {audioCalibration}, video calibration: {videoCalibration}, song offset: {songOffset}");
#endif

            // Loaded, enable updates
            enabled = true;
            IsSongStarted = true;
            _songStarted?.Invoke();
        }

        private void Update()
        {
            // Pause/unpause
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (IsPractice && !PracticeManager.HasSelectedSection)
                {
                    return;
                }

                SetPaused(!_songRunner.Paused);
            }

            // Toggle debug text
            if (Keyboard.current.ctrlKey.isPressed && Keyboard.current.tabKey.wasPressedThisFrame)
            {
                _isShowDebugText = !_isShowDebugText;

                _debugText.gameObject.SetActive(_isShowDebugText);
            }

            // Skip the rest if paused
            if (_songRunner.Paused) return;

            // Update handlers
            _songRunner.Update();
            BeatEventHandler.Update(_songRunner.SongTime);

            // Update players
            int totalScore = 0;
            int totalCombo = 0;
            foreach (var player in _players)
            {
                player.UpdateWithTimes(_songRunner.InputTime);

                totalScore += player.Score;
                totalCombo += player.Combo;
            }

            BandScore = totalScore;
            BandCombo = totalCombo;

            // Get the band stars
            double totalStars = 0f;
            foreach (var player in _players)
            {
                var thresh = player.StarScoreThresholds;
                for (int i = 0; i < thresh.Length; i++)
                {
                    // Skip until we reach the progressing threshold
                    if (player.Score > thresh[i])
                    {
                        if (i == thresh.Length - 1)
                        {
                            totalStars += 6f;
                        }

                        continue;
                    }

                    // Otherwise, get the progress.
                    // There is at least this amount of stars.
                    totalStars += i;

                    // Then, we just gotta get the progress into the next star.
                    int bound = i != 0 ? thresh[i - 1] : 0;
                    totalStars += (double) (player.Score - bound) / (thresh[i] - bound);

                    break;
                }
            }

            BandStars = totalStars / _players.Count;

            // Debug text
            // Note: this must come last in the update sequence!
            // Any updates happening after this will not reflect until the next frame
            if (_isShowDebugText)
            {
                _debugText.text = null;

                if (_players[0] is FiveFretPlayer fiveFretPlayer)
                {
                    byte buttonMask = fiveFretPlayer.Engine.State.ButtonMask;
                    int noteIndex = fiveFretPlayer.Engine.State.NoteIndex;
                    var ticksPerEight = fiveFretPlayer.Engine.State.TicksEveryEightMeasures;
                    double starPower = fiveFretPlayer.Engine.EngineStats.StarPowerAmount;

                    _debugText.text +=
                        $"Note index: {noteIndex}\n" +
                        $"Buttons: {buttonMask}\n" +
                        $"Star Power: {starPower:0.0000}\n" +
                        $"TicksPerEight: {ticksPerEight}\n";
                }
                else if (_players[0] is DrumsPlayer drumsPlayer)
                {
                    int noteIndex = drumsPlayer.Engine.State.NoteIndex;

                    _debugText.text +=
                        $"Note index: {noteIndex}\n";
                }

                _debugText.text +=
                    $"Song time: {_songRunner.SongTime:0.000000}\n" +
                    $"Input time: {_songRunner.InputTime:0.000000}\n" +
                    $"Pause time: {_songRunner.PauseStartTime:0.000000}\n" +
                    $"Time difference: {_songRunner.SyncInputTime - _songRunner.SyncSongTime:0.000000}\n" +
                    $"Sync start delta: {_songRunner.SyncStartDelta:0.000000}\n" +
                    $"Speed adjustment: {_songRunner.SyncSpeedAdjustment:0.00}\n" +
                    $"Speed multiplier: {_songRunner.SyncSpeedMultiplier}\n" +
                    $"Input base: {_songRunner.InputTimeBase:0.000000}\n" +
                    $"Input offset: {_songRunner.InputTimeOffset:0.000000}\n";
            }
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

            var players = new List<YargPlayer>();
            foreach (var frame in Replay.Frames)
            {
                var yargPlayer = new YargPlayer(frame.PlayerInfo.Profile, null, false);
                yargPlayer.OverrideColorProfile(Replay.ColorProfiles[frame.PlayerInfo.ColorProfileId]);

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
            });

            if (_loadState != LoadFailureState.None)
                return;

            BeatEventHandler = new(Chart.SyncTrack);
            _chartLoaded?.Invoke(Chart);
        }

        private async UniTask LoadAudio()
        {
            bool isYargSong = Song.Source.Str.ToLowerInvariant() == "yarg";
            GlobalVariables.AudioManager.Options.UseMinimumStemVolume = isYargSong;

            await UniTask.RunOnThreadPool(() =>
            {
                try
                {
                    Song.LoadAudio(GlobalVariables.AudioManager, GlobalVariables.Instance.SongSpeed);
                    SongLength = GlobalVariables.AudioManager.AudioLengthD;
                    GlobalVariables.AudioManager.SongEnd += OnAudioEnd;
                }
                catch (Exception ex)
                {
                    _loadState = LoadFailureState.Error;
                    _loadFailureMessage = "Failed to load audio!";
                    Debug.LogException(ex, this);
                }
            });

            if (_loadState != LoadFailureState.None)
                return;

            _songLoaded?.Invoke();
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
                if (player.SittingOut) continue;

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
                    var basePlayer = playerObject.GetComponent<TrackPlayer>();
                    var trackView = _trackViewManager.CreateTrackView(basePlayer, player);
                    basePlayer.Initialize(index, player, Chart, trackView);
                    _players.Add(basePlayer);
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
            }
        }

        public void SetSongTime(double time, double delayTime = SONG_START_DELAY)
            => _songRunner.SetSongTime(time, delayTime);

        public void SetSongSpeed(float speed) => _songRunner.SetSongSpeed(speed);
        public void AdjustSongSpeed(float deltaSpeed) => _songRunner.SetSongSpeed(deltaSpeed);

        public void Pause(bool showMenu = true)
        {
            if (_songRunner.Paused) return;

            if (showMenu)
            {
                if (GlobalVariables.Instance.IsReplay)
                {
                    _pauseMenu.PushMenu(PauseMenuManager.Menu.ReplayPause);
                }
                else if (GlobalVariables.Instance.IsPractice)
                {
                    _pauseMenu.PushMenu(PauseMenuManager.Menu.PracticePause);
                }
                else
                {
                    _pauseMenu.PushMenu(PauseMenuManager.Menu.QuickPlayPause);
                }
            }

            if (!IsReplay)
            {
                _debugText.gameObject.SetActive(false);
            }

            _songRunner.Pause();

            // Pause the background/venue
            Time.timeScale = 0f;
            BackgroundManager.SetPaused(true);
        }

        public void Resume(bool inputCompensation = true)
        {
            if (!_songRunner.Paused) return;

            _pauseMenu.gameObject.SetActive(false);

            // Unpause the background/venue
            Time.timeScale = 1f;
            BackgroundManager.SetPaused(false);

            _isReplaySaved = false;

            _debugText.gameObject.SetActive(_isShowDebugText);

            _songRunner.Resume(inputCompensation);
        }

        public void SetPaused(bool paused)
        {
            _songRunner.SetPaused(paused);
            GameStateFetcher.SetPaused(paused);
        }

        public void OverridePauseTime(double pauseTime = -1) => _songRunner.OverridePauseTime(pauseTime);

        public double GetRelativeInputTime(double timeFromInputSystem)
            => _songRunner.GetRelativeInputTime(timeFromInputSystem);

        public double GetCalibratedRelativeInputTime(double timeFromInputSystem)
            => _songRunner.GetRelativeInputTime(timeFromInputSystem);

        private void EndSong()
        {
            if (IsPractice)
            {
                PracticeManager.ResetPractice();
                return;
            }

            if (!IsReplay)
            {
                _isReplaySaved = false;
                SaveReplay(Song.SongLengthSeconds);
            }

            GlobalVariables.AudioManager.UnloadSong();

            GlobalVariables.Instance.ScoreScreenStats = new ScoreScreenStats
            {
                PlayerScores = _players.Select(player => new PlayerScoreCard
                {
                    Player = player.Player,
                    Stats = player.Stats
                }).ToArray(),
                BandScore = BandScore,
                BandStars = (int) BandStars
            };

            GlobalVariables.Instance.IsReplay = false;
            GlobalVariables.Instance.LoadScene(SceneIndex.Score);
        }

        public void ForceQuitSong()
        {
            GlobalVariables.AudioManager.UnloadSong();

            GlobalVariables.Instance.IsReplay = false;
            GlobalVariables.Instance.LoadScene(SceneIndex.Menu);
        }

        public void SaveReplay(double length)
        {
            var realPlayers = _players.Where(player => !player.Player.Profile.IsBot).ToList();

            if (_isReplaySaved || realPlayers.Count == 0)
            {
                return;
            }

            var replay = ReplayContainer.CreateNewReplay(Song, realPlayers, length);
            var entry = ReplayContainer.CreateEntryFromReplayFile(new ReplayFile(replay));

            entry.ReplayFile = entry.GetReplayName();

            ReplayIO.WriteReplay(Path.Combine(ReplayContainer.ReplayDirectory, entry.ReplayFile), replay);

            Debug.Log("Wrote replay");
            _isReplaySaved = true;
        }

        private void OnAudioEnd()
        {
            if (IsPractice)
            {
                PracticeManager.ResetPractice();
                return;
            }

            if (IsReplay)
            {
                Pause(false);
                return;
            }

            EndSong();
        }

        private void OnNavigationEvent(NavigationContext context)
        {
            switch (context.Action)
            {
                // Pause
                case MenuAction.Start:
                    if (IsPractice && !PracticeManager.HasSelectedSection)
                    {
                        return;
                    }

                    SetPaused(!_songRunner.Paused);
                    break;
            }
        }
    }
}