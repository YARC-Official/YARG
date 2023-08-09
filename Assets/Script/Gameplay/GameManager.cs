using System;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using YARG.Core;
using YARG.Core.Chart;
using YARG.Core.Game;
using YARG.Core.Input;
using YARG.Core.Replays;
using YARG.Core.Replays.IO;
using YARG.Gameplay.HUD;
using YARG.Gameplay.Player;
using YARG.Input;
using YARG.Menu.Navigation;
using YARG.Menu.Persistent;
using YARG.Player;
using YARG.Replays;
using YARG.Settings;
using YARG.Song;

namespace YARG.Gameplay
{
    public class GameManager : MonoBehaviour
    {
        private const double SONG_START_DELAY = 2;

        [Header("References")]
        [SerializeField]
        private TrackViewManager _trackViewManager;

        [SerializeField]
        private PauseMenuManager _pauseMenu;

        [SerializeField]
        private TextMeshProUGUI _debugText;

        [Header("Instrument Prefabs")]
        [SerializeField]
        private GameObject fiveFretGuitarPrefab;

        [SerializeField]
        private GameObject sixFretGuitarPrefab;

        [SerializeField]
        private GameObject fourLaneDrumsPrefab;

        [SerializeField]
        private GameObject fiveLaneDrumsPrefab;

        [SerializeField]
        private GameObject proGuitarPrefab;

        private SongChart _chart;

        private double _pauseStartTime;

        private List<BasePlayer> _players;
        private List<Beatline>   _beats;

        private IReadOnlyList<YargPlayer> _yargPlayers;

        private bool _loadFailure;
        private string _loadFailureMessage;

        private UniTask _syncAudio = UniTask.CompletedTask;

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
                var chart = _chart;
                if (chart != null)
                    value?.Invoke(chart);
            }
            remove => _chartLoaded -= value;
        }

        public PracticeManager PracticeManager { get; private set; }

        public SongEntry Song { get; private set; }

        private float _syncSpeedAdjustment = 0f;
        private int _syncSpeedMultiplier = 0;
        private double _syncStartDelta;

        public float SelectedSongSpeed { get; private set; }
        public float ActualSongSpeed => SelectedSongSpeed + _syncSpeedAdjustment;

        public double SongLength { get; private set; }

        public double SongStartDelay => SONG_START_DELAY * SelectedSongSpeed;

        public double AudioCalibration => -SettingsManager.Settings.AudioCalibration.Data / 1000.0;

        /// <summary>
        /// The time into the song <b>without</b> accounting for calibration.<br/>
        /// This is updated every frame.
        /// </summary>
        public double RealSongTime { get; private set; }

        /// <summary>
        /// The time into the song <b>accounting</b> for calibration.<br/>
        /// This is updated every frame.
        /// </summary>
        public double SongTime => RealSongTime + AudioCalibration;

        /// <summary>
        /// The input time that is considered to be 0.
        /// Applied before song speed is factored in.
        /// </summary>
        public static double InputTimeOffset { get; private set; }
        /// <summary>
        /// The base time added on to relative time to get the real current input time.
        /// Applied after song speed is.
        /// </summary>
        public static double InputTimeBase { get; private set; }

        /// <summary>
        /// The current input update time, accounting for song speed, <b>and for</b> calibration.
        /// </summary>
        // Remember that calibration is already accounted for by the input offset time
        public double InputTime { get; private set; }

        /// <summary>
        /// The current input update time, accounting for song speed, but <b>not</b> for calibration.
        /// </summary>
        // Uses the selected song speed and not the actual song speed,
        // audio is synced to the inputs and not vice versa
        public double RealInputTime => InputTime - AudioCalibration;

        public bool IsReplay   { get; private set; }
        public bool IsPractice { get; private set; }

        public bool Paused { get; private set; }

        public int BandScore { get; private set; }
        public int BandCombo { get; private set; }

        public Replay Replay { get; private set; }

        public IReadOnlyList<BasePlayer> Players => _players;

        private bool _isShowDebugText;

        private void Awake()
        {
            PracticeManager = GetComponent<PracticeManager>();
            _yargPlayers = PlayerContainer.Players;

            Song = GlobalVariables.Instance.CurrentSong;
            IsReplay = GlobalVariables.Instance.IsReplay;
            IsPractice = GlobalVariables.Instance.IsPractice;
            SelectedSongSpeed = GlobalVariables.Instance.SongSpeed;

            Navigator.Instance.PopAllSchemes();

            if (Song is null)
            {
                Debug.Assert(false, "Null song set when loading gameplay!");
                GlobalVariables.Instance.LoadScene(SceneIndex.Menu);
                return;
            }
        }

        private void OnDestroy()
        {
            Navigator.Instance.NavigationEvent -= OnNavigationEvent;
            GlobalVariables.AudioManager.SongEnd -= OnAudioEnd;
        }

        private async UniTask Start()
        {
            // Disable until everything's loaded
            enabled = false;

            // Load song
            if (IsReplay)
                LoadingManager.Instance.Queue(LoadReplay, "Loading replay...");
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

            // Set start time
            SetSongTime(0);

            // Loaded, enable updates
            enabled = true;

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
        }

        private void Update()
        {
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                SetPaused(!Paused);
            }

            if (Paused)
            {
                return;
            }

            // Calculate song time
            if (RealSongTime < 0.0)
            {
                // Drive song time using input time until it's time to start the audio
                RealSongTime = RealInputTime;
                if (RealSongTime >= 0.0)
                {
                    // Start audio
                    GlobalVariables.AudioManager.Play();
                    // Seek to calculated time to keep everything in sync
                    GlobalVariables.AudioManager.SetPosition(RealSongTime);
                }
            }
            else
            {
                RealSongTime = GlobalVariables.AudioManager.CurrentPositionD;
                // Sync if needed
                SyncAudio();
            }

            // Update input time
            InputTime = GetRelativeInputTime(InputManager.CurrentUpdateTime);

            if (Keyboard.current.ctrlKey.isPressed && Keyboard.current.tabKey.wasPressedThisFrame)
            {
                _isShowDebugText = !_isShowDebugText;

                _debugText.gameObject.SetActive(_isShowDebugText);
            }

            if (_isShowDebugText)
            {
                byte buttonMask = ((FiveFretPlayer) _players[0]).Engine.State.ButtonMask;
                int noteIndex = ((FiveFretPlayer) _players[0]).Engine.State.NoteIndex;
                _debugText.text = $"Note index: {noteIndex}\nButtons: {buttonMask}\n"
                    + $"Input time: {InputTime:0.000000}\nSong time: {SongTime:0.000000}\nTime difference: {InputTime - SongTime:0.000000}\n"
                    + $"Speed adjustment: {_syncSpeedAdjustment:0.00}\nSpeed multiplier: {_syncSpeedMultiplier}\n"
                    + $"Sync start delta: {_syncStartDelta:0.000000}";
            }

            int totalScore = 0;
            int totalCombo = 0;
            foreach (var player in _players)
            {
                player.UpdateWithTimes(InputTime, SongTime);

                totalScore += player.Score;
                totalCombo += player.Combo;
            }

            BandScore = totalScore;
        }

        private void SyncAudio()
        {
            const double INITIAL_SYNC_THRESH = 0.015;
            const double ADJUST_SYNC_THRESH = 0.005;
            const float SPEED_ADJUSTMENT = 0.05f;

            double inputTime = InputTime;
            double audioTime = SongTime;

            // Account for song speed
            double initialThreshold = INITIAL_SYNC_THRESH * SelectedSongSpeed;
            double adjustThreshold = ADJUST_SYNC_THRESH * SelectedSongSpeed;

            // Check the difference between input and audio times
            double delta = inputTime - audioTime;
            double deltaAbs = Math.Abs(delta);
            // Don't sync if below the initial sync threshold, and we haven't adjusted the speed
            if (_syncSpeedMultiplier == 0 && deltaAbs < initialThreshold)
                return;

            // We're now syncing, determine how much to adjust the song speed by
            int speedMultiplier = (int)Math.Round(delta / initialThreshold);
            if (speedMultiplier == 0)
                speedMultiplier = delta > 0 ? 1 : -1;

            // Only change speed when the multiplier changes
            if (_syncSpeedMultiplier != speedMultiplier)
            {
                if (_syncSpeedMultiplier == 0)
                {
                    _syncStartDelta = delta;
                }

                _syncSpeedMultiplier = speedMultiplier;

                float adjustment = SPEED_ADJUSTMENT * speedMultiplier;
                if (!Mathf.Approximately(adjustment, _syncSpeedAdjustment))
                {
                    _syncSpeedAdjustment = adjustment;
                    GlobalVariables.AudioManager.SetSpeed(ActualSongSpeed);
                }
            }

            // No change in speed, check if we're below the threshold
            if (deltaAbs < adjustThreshold ||
                // Also check if we overshot and passed 0
                (delta > 0.0 && _syncStartDelta < 0.0) ||
                (delta < 0.0 && _syncStartDelta > 0.0))
            {
                _syncStartDelta = 0;
                _syncSpeedMultiplier = 0;
                _syncSpeedAdjustment = 0f;
                GlobalVariables.AudioManager.SetSpeed(ActualSongSpeed);
            }
        }

        private async UniTask LoadReplay()
        {
            string checksum = GlobalVariables.Instance.CurrentReplay.SongChecksum;
            Replay replay = null;
            ReplayReadResult result;
            try
            {
                result = await UniTask.RunOnThreadPool(() => ReplayContainer.LoadReplayFile(
                GlobalVariables.Instance.CurrentReplay, out replay));
            }
            catch (Exception ex)
            {
                _loadFailure = true;
                _loadFailureMessage = "Failed to load replay!";
                Debug.LogException(ex, this);
                return;
            }

            Song = GlobalVariables.Instance.Container.SongsByHash[GlobalVariables.Instance.CurrentReplay.SongChecksum][0];
            if (Song is null || result != ReplayReadResult.Valid)
            {
                _loadFailure = true;
                _loadFailureMessage = "Failed to load replay!";
                return;
            }

            Replay = replay;

            var players = new List<YargPlayer>();
            foreach (var frame in Replay.Frames)
            {
                var profile = new YargProfile
                {
                    Name = frame.PlayerName,
                    GameMode = frame.Instrument.ToGameMode(),
                    Instrument = frame.Instrument,
                    Difficulty = frame.Difficulty,
                    NoteSpeed = 7,
                };

                players.Add(new YargPlayer(profile, null, false));
            }

            _yargPlayers = players;
        }

        private async UniTask LoadChart()
        {
            await UniTask.RunOnThreadPool(() =>
            {
                // Load chart
                string notesFile = Path.Combine(Song.Location, Song.NotesFile);
                Debug.Log($"Loading chart file {notesFile}");
                try
                {
                    _chart = SongChart.FromFile(ParseSettings.Default, notesFile);
                }
                catch (Exception ex)
                {
                    _loadFailure = true;
                    _loadFailureMessage = "Failed to load chart!";
                    Debug.LogException(ex, this);
                    return;
                }

                // Ensure sync track is present
                var syncTrack = _chart.SyncTrack;
                if (syncTrack.Beatlines is null or { Count: < 1 })
                    _chart.SyncTrack.GenerateBeatlines(_chart.GetLastTick());

                _beats = _chart.SyncTrack.Beatlines;

                // Set length of the final section
                if (_chart.Sections.Count > 0) {
                    uint lastTick = _chart.GetLastTick();
                    _chart.Sections[^1].TickLength = lastTick;
                    _chart.Sections[^1].TimeLength = _chart.SyncTrack.TickToTime(lastTick);
                }
            });

            if (_loadFailure)
                return;

            _chartLoaded?.Invoke(_chart);
        }

        private async UniTask LoadAudio()
        {
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
        }

        private void CreatePlayers()
        {
            _players = new List<BasePlayer>();

            int index = -1;
            foreach (var player in _yargPlayers)
            {
                index++;
                var prefab = player.Profile.GameMode switch
                {
                    GameMode.FiveFretGuitar => fiveFretGuitarPrefab,
                    GameMode.SixFretGuitar  => sixFretGuitarPrefab,
                    GameMode.FourLaneDrums  => fourLaneDrumsPrefab,
                    GameMode.FiveLaneDrums  => fiveLaneDrumsPrefab,
                    GameMode.ProGuitar      => proGuitarPrefab,

                    _ => null
                };
                if (prefab == null)
                {
                    continue;
                }

                var playerObject = Instantiate(prefab, new Vector3(index * 25f, 100f, 0f), prefab.transform.rotation);

                // Setup player
                var basePlayer = playerObject.GetComponent<BasePlayer>();
                var trackView = _trackViewManager.CreateTrackView(basePlayer);
                basePlayer.Initialize(index, player, _chart, trackView);
                _players.Add(basePlayer);
            }
        }

        public double GetRelativeInputTime(double timeFromInputSystem)
        {
            return InputTimeBase + ((timeFromInputSystem - InputTimeOffset) * SelectedSongSpeed);
        }

        private void SetInputBase(double inputBase)
        {
            InputTimeBase = inputBase;
            InputTimeOffset = InputManager.CurrentUpdateTime;
        }

        public void SetSongTime(double time, double delayTime = SONG_START_DELAY)
        {
            // Account for song speed and calibration
            delayTime *= SelectedSongSpeed;
            time -= AudioCalibration;

            // Seek time
            // Doesn't account for audio calibration for better audio syncing
            // since seeking is slightly delayed
            double seekTime = time - delayTime;

            // Set input offsets
            SetInputBase(seekTime);

            // Set audio/song time
            RealSongTime = seekTime;

            // Audio seeking; cannot go negative
            if (seekTime < 0) seekTime = 0;
            GlobalVariables.AudioManager.SetPosition(seekTime);

            Debug.Log($"Set song time to {time}.\nSeek time: {seekTime}, input time: {InputTime} " +
                $"(base: {InputTimeBase}, offset: {InputTimeOffset}, absolute: {InputManager.CurrentUpdateTime})");
        }

        private async UniTask SetSongSpeedTask(float speed)
        {
            // 10% - 4995%, we reserve 5% so that audio syncing can still function
            speed = Math.Clamp(speed, 10 / 100f, 4995 / 100f);

            // Set speed; save old for input offset compensation
            SelectedSongSpeed = speed;

            // Set based on the actual song speed, so as to not break resyncing
            GlobalVariables.AudioManager.SetSpeed(ActualSongSpeed);

            // Wait until next frame to apply input offset,
            // seems to help avoid sudden jumps in speed
            await UniTask.NextFrame();

            // Adjust input offset, otherwise input time will desync
            SetInputBase(InputTime);

            Debug.Log($"Set song speed to {speed:0.00}.\n"
                + $"Input time: {InputTime:0.000000}, song time: {SongTime:0.000000}");
        }

        public void SetSongSpeed(float speed)
        {
            SetSongSpeedTask(speed).Forget();
        }

        public void AdjustSongSpeed(float deltaSpeed) => SetSongSpeed(SelectedSongSpeed + deltaSpeed);

        public void Pause(bool showMenu = true)
        {
            if (Paused) return;

            Paused = true;

            if (showMenu)
            {
                if (GlobalVariables.Instance.IsPractice)
                {
                    _pauseMenu.PushMenu(PauseMenuManager.Menu.PracticePause);
                }
                else
                {
                    _pauseMenu.PushMenu(PauseMenuManager.Menu.QuickPlayPause);
                }
            }

            _debugText.gameObject.SetActive(false);

            _pauseStartTime = InputTime;
            GlobalVariables.AudioManager.Pause();
        }

        public void Resume(bool inputCompensation = true)
        {
            if (!Paused) return;

            Paused = false;
            _pauseMenu.gameObject.SetActive(false);

            _debugText.gameObject.SetActive(_isShowDebugText);

            if (inputCompensation)
                SetInputBase(_pauseStartTime);

            if (RealSongTime >= 0)
                GlobalVariables.AudioManager.Play();
        }

        public void SetPaused(bool paused)
        {
            if (paused)
                Pause();
            else
                Resume();
        }

        private void EndSong()
        {
            if (IsPractice)
            {
                PracticeManager.ResetPractice();
            }
            if (!IsReplay)
            {
                var replay = ReplayContainer.CreateNewReplay(Song, _players);
                var entry = new ReplayEntry
                {
                    SongName = replay.SongName,
                    ArtistName = replay.ArtistName,
                    CharterName = replay.CharterName,
                    BandScore = replay.BandScore,
                    Date = replay.Date,
                    SongChecksum = replay.SongChecksum,
                    PlayerCount = replay.PlayerCount,
                    PlayerNames = replay.PlayerNames,
                    GameVersion = replay.Header.GameVersion,
                };

                entry.ReplayFile = entry.GetReplayName();

                ReplayIO.WriteReplay(Path.Combine(ReplayContainer.ReplayDirectory, entry.ReplayFile), replay);

                Debug.Log("Wrote replay");
            }

            QuitSong();
        }

        public void QuitSong()
        {
            GlobalVariables.AudioManager.UnloadSong();

            GlobalVariables.Instance.IsReplay = false;
            GlobalVariables.Instance.LoadScene(SceneIndex.Menu);
        }

        private void OnAudioEnd()
        {
            if (IsPractice)
            {
                PracticeManager.ResetPractice();
                return;
            }

            GlobalVariables.AudioManager.SongEnd -= OnAudioEnd;
            EndSong();
        }

        private void OnNavigationEvent(NavigationContext context)
        {
            switch (context.Action)
            {
                // Pause
                case MenuAction.Start:
                    SetPaused(!Paused);
                    break;
            }
        }
    }
}