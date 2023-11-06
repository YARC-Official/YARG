using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using YARG.Core;
using YARG.Core.Chart;
using YARG.Core.Input;
using YARG.Core.Replays;
using YARG.Core.Song;
using YARG.Gameplay.HUD;
using YARG.Gameplay.Player;
using YARG.Integration;
using YARG.Menu.Navigation;
using YARG.Menu.Persistent;
using YARG.Menu.ScoreScreen;
using YARG.Player;
using YARG.Replays;
using YARG.Helpers.Extensions;
using YARG.Scores;

namespace YARG.Gameplay
{
    [DefaultExecutionOrder(-1)]
    public partial class GameManager : MonoBehaviour
    {
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

        private bool   _loadFailure;
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

        public PracticeManager  PracticeManager  { get; private set; }
        public BeatEventManager BeatEventManager { get; private set; }
        public BackgroundManager BackgroundManager { get; private set; }

        public SongMetadata Song  { get; private set; }
        public SongChart    Chart { get; private set; }

        public float SelectedSongSpeed { get; private set; }
        public float ActualSongSpeed   => SelectedSongSpeed + _syncSpeedAdjustment;

        public double SongLength { get; private set; }

        public bool IsReplay   { get; private set; }
        public bool IsPractice { get; private set; }

        public bool Paused { get; private set; }

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
            BeatEventManager = GetComponent<BeatEventManager>();
            BackgroundManager = GetComponent<BackgroundManager>();

            _yargPlayers = PlayerContainer.Players;

            Song = GlobalVariables.Instance.CurrentSong;
            IsReplay = GlobalVariables.Instance.IsReplay;
            IsPractice = GlobalVariables.Instance.IsPractice && !IsReplay;
            SelectedSongSpeed = GlobalVariables.Instance.SongSpeed;

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
            UninitializeTime();

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

            if (_loadFailure)
            {
                Debug.LogError(_loadFailureMessage);
                ToastManager.ToastError(_loadFailureMessage);
                GlobalVariables.Instance.LoadScene(SceneIndex.Menu);
                return;
            }

            // Spawn players
            CreatePlayers();

            // Initialize time stuff
            InitializeTime();

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
            Debug.Log($"Audio calibration: {AudioCalibration}, song offset: {SongOffset}");
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

                SetPaused(!Paused);
            }

            // Toggle debug text
            if (Keyboard.current.ctrlKey.isPressed && Keyboard.current.tabKey.wasPressedThisFrame)
            {
                _isShowDebugText = !_isShowDebugText;

                _debugText.gameObject.SetActive(_isShowDebugText);
            }

            // Skip the rest if paused
            if (Paused) return;

            // Update timing info
            UpdateTimes();

            // Update players
            int totalScore = 0;
            int totalCombo = 0;
            foreach (var player in _players)
            {
                player.UpdateWithTimes(InputTime);

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
                    $"Song time: {SongTime:0.000000}\n" +
                    $"Input time: {InputTime:0.000000}\n" +
                    $"Pause time: {PauseStartTime:0.000000}\n" +
                    $"Time difference: {InputTime - SongTime:0.000000}\n" +
                    $"Sync start delta: {_syncStartDelta:0.000000}\n" +
                    $"Speed adjustment: {_syncSpeedAdjustment:0.00}\n" +
                    $"Speed multiplier: {_syncSpeedMultiplier}\n" +
                    $"Input base: {InputTimeBase:0.000000}\n" +
                    $"Input offset: {InputTimeOffset:0.000000}\n";
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
                _loadFailure = true;
                _loadFailureMessage = "Failed to load replay!";
                Debug.LogException(ex, this);
                return;
            }

            Song = GlobalVariables.Instance.SongContainer.SongsByHash[
                GlobalVariables.Instance.CurrentReplay.SongChecksum][0];
            if (Song is null || result != ReplayReadResult.Valid)
            {
                _loadFailure = true;
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
                    _loadFailure = true;
                    _loadFailureMessage = "Failed to load chart!";
                    Debug.LogException(ex, this);
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

            if (_loadFailure) return;

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
                    Song.LoadAudio(GlobalVariables.AudioManager, SelectedSongSpeed);
                    SongLength = GlobalVariables.AudioManager.AudioLengthD;
                    GlobalVariables.AudioManager.SongEnd += OnAudioEnd;
                }
                catch (Exception ex)
                {
                    _loadFailure = true;
                    _loadFailureMessage = "Failed to load audio!";
                    Debug.LogException(ex, this);
                }
            });

            if (_loadFailure) return;

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

        public void SetSongSpeed(float speed)
        {
            _pauseSync = true;
            _finishedSyncing.WaitOne();

            // 10% - 4995%, we reserve 5% so that audio syncing can still function
            speed = Math.Clamp(speed, 10 / 100f, 4995 / 100f);

            // Set speed; save old for input offset compensation
            SelectedSongSpeed = speed;

            // Set based on the actual song speed, so as to not break resyncing
            GlobalVariables.AudioManager.SetSpeed(ActualSongSpeed);

            // Adjust input offset, otherwise input time will desync
            SetInputBase(InputTime);

            _pauseSync = false;

#if UNITY_EDITOR
            Debug.Log($"Set song speed to {speed:0.00}.\n"
                + $"Input time: {InputTime:0.000000}, song time: {SongTime:0.000000}");
#endif
        }

        public void AdjustSongSpeed(float deltaSpeed) => SetSongSpeed(SelectedSongSpeed + deltaSpeed);

        public void Pause(bool showMenu = true)
        {
            if (Paused) return;

            Paused = true;

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

            PauseStartTime = RealInputTime;
            GlobalVariables.AudioManager.Pause();

            // Pause the background/venue
            Time.timeScale = 0f;
            BackgroundManager.SetPaused(true);

#if UNITY_EDITOR
            Debug.Log($"Paused at song time {SongTime:0.000000} (real: {RealSongTime:0.000000}), input time {InputTime:0.000000} (real: {RealInputTime:0.000000}).");
#endif
        }

        public void Resume(bool inputCompensation = true)
        {
            if (!Paused) return;

            Paused = false;
            _pauseMenu.gameObject.SetActive(false);

            // Unpause the background/venue
            Time.timeScale = 1f;
            BackgroundManager.SetPaused(false);

            _isReplaySaved = false;

            _debugText.gameObject.SetActive(_isShowDebugText);

            if (inputCompensation)
            {
                SetInputBase(PauseStartTime);
            }

            if (RealSongTime >= SongOffset)
            {
                GlobalVariables.AudioManager.Play();
            }

#if UNITY_EDITOR
            Debug.Log($"Resumed at song time {SongTime:0.000000} (real: {RealSongTime:0.000000}), input time {InputTime:0.000000} (real: {RealInputTime:0.000000}).");
#endif
        }

        public void SetPaused(bool paused)
        {
            if (paused)
            {
                Pause();
            }
            else
            {
                Resume();
            }

            GameStateFetcher.SetPaused(paused);
        }

        public void OverridePauseTime(double pauseTime = -1)
        {
            if (!Paused)
            {
                return;
            }

            if (pauseTime < 0)
                pauseTime = RealInputTime;

            PauseStartTime = pauseTime;
        }

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

            UninitializeTime();
            GlobalVariables.AudioManager.UnloadSong();

            // Pass the score info to the stats screen
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

            // Get all of the individual player score entries
            var playerEntries = new List<PlayerScoreRecord>();
            foreach (var player in _players)
            {
                var profile = player.Player.Profile;

                playerEntries.Add(new PlayerScoreRecord
                {
                    PlayerId = profile.Id,

                    Instrument = profile.CurrentInstrument,
                    Difficulty = profile.CurrentDifficulty,

                    Score = player.Score,
                    Stars = StarAmountHelper.GetStarsFromInt(player.Stats.Stars),

                    NotesHit = player.Stats.NotesHit,
                    NotesMissed = player.Stats.NotesMissed,
                    IsFc = player.IsFc
                });
            }

            // Record the score into the database
            ScoreContainer.RecordScore(new GameRecord
            {
                Date = DateTime.Now,

                SongChecksum = Song.Hash.ToString(),
                SongName = Song.Name,
                SongArtist = Song.Artist,
                SongCharter = Song.Charter,

                BandScore = BandScore,
                BandStars = StarAmountHelper.GetStarsFromInt((int) BandStars),

                SongSpeed = SelectedSongSpeed
            }, playerEntries);

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
            bool realPlayerActive = _players.Any(player => !player.Player.Profile.IsBot);

            if (_isReplaySaved || !realPlayerActive)
            {
                return;
            }

            var replay = ReplayContainer.CreateNewReplay(Song, _players, length);
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

                    SetPaused(!Paused);
                    break;
            }
        }
    }
}