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
        private GameObject _pauseMenu;

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
        /// The current input update time, accounting for song speed, <b>and for</b> calibration.
        /// </summary>
        // Remember that calibration is already accounted for by the input offset time
        public double InputTime => InputManager.RelativeUpdateTime * SelectedSongSpeed;

        /// <summary>
        /// The current input time <b>at this instant</b>, accounting for song speed, <b>and for</b> calibration.
        /// </summary>
        public double InstantInputTime => InputManager.RelativeInputTime * SelectedSongSpeed;

        /// <summary>
        /// The current input update time, accounting for song speed, but <b>not</b> for calibration.
        /// </summary>
        // Uses the selected song speed and not the actual song speed,
        // audio is synced to the inputs and not vice versa
        public double RealInputTime => InputTime - AudioCalibration;

        /// <summary>
        /// The current input time <b>at this instant</b>, accounting for song speed, but <b>not</b> for calibration.
        /// </summary>
        public double RealInstantInputTime => InstantInputTime - AudioCalibration;

        public bool IsReplay   { get; private set; }
        public bool IsPractice { get; private set; }

        public bool Paused { get; private set; }

        public int BandScore { get; private set; }
        public int BandCombo { get; private set; }

        public Replay Replay { get; private set; }

        public IReadOnlyList<BasePlayer> Players => _players;

        private void Awake()
        {
            PracticeManager = GetComponent<PracticeManager>();
            _yargPlayers = PlayerContainer.Players;

            Song = GlobalVariables.Instance.CurrentSong;
            IsReplay = GlobalVariables.Instance.IsReplay;
            IsPractice = GlobalVariables.Instance.IsPractice;
            SelectedSongSpeed = GlobalVariables.Instance.SongSpeed;

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
                ChangeSection();
            }
            else
            {
#if UNITY_EDITOR
                // Show debug info
                _debugText.gameObject.SetActive(true);
#endif
                Destroy(_pauseMenu.transform.Find("Background/ChangeSection").gameObject);
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
            }

#if UNITY_EDITOR
            byte buttonMask = ((FiveFretPlayer) _players[0]).Engine.State.ButtonMask;
            int noteIndex = ((FiveFretPlayer) _players[0]).Engine.State.NoteIndex;
            _debugText.text = $"Note index: {noteIndex}\nButtons: {buttonMask}\nInput time: {InputTime:0.000000}\nSong time: {SongTime:0.000000}";
#endif

            if (_syncAudio.Status != UniTaskStatus.Pending) _syncAudio = SyncAudio();

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

        private async UniTask SyncAudio()
        {
            const double INITIAL_SYNC_THRESH = 0.015;
            const double ADJUST_SYNC_THRESH = 0.005;
            const float SPEED_ADJUSTMENT = 0.05f;

            // DON'T use any "SongTime" variables for this because that is
            // updated every frame. This is async.
            double inputTime = RealInstantInputTime;
            double audioTime = GlobalVariables.AudioManager.CurrentPositionD;

            if (audioTime < 0.0) return;

            double delta = inputTime - audioTime;
            if (Math.Abs(delta) < INITIAL_SYNC_THRESH) return;

            Debug.Log($"Resyncing audio position. Input: {inputTime}, audio: {audioTime}, delta: {delta}");

            _syncSpeedAdjustment = delta > 0.0 ? SPEED_ADJUSTMENT : -SPEED_ADJUSTMENT;
            GlobalVariables.AudioManager.SetSpeed(ActualSongSpeed);

            await UniTask.WaitUntil(() =>
            {
                double newDelta = RealInstantInputTime - GlobalVariables.AudioManager.CurrentPositionD;

                // Make sure to use a lower threshold so we don't have to *constantly*
                // sync things up.
                return Math.Abs(newDelta) < ADJUST_SYNC_THRESH ||
                    // Detect overshooting
                    (delta > 0.0 && newDelta < 0.0) ||
                    (delta < 0.0 && newDelta > 0.0);
            });
            _syncSpeedAdjustment = 0f;
            GlobalVariables.AudioManager.SetSpeed(ActualSongSpeed);

            inputTime = RealInstantInputTime;
            audioTime = GlobalVariables.AudioManager.CurrentPositionD;
            double finalDelta = inputTime - audioTime;
            Debug.Log($"Audio synced. Input: {inputTime}, audio: {audioTime}, delta: {finalDelta}");
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

            Song = SongContainer.SongsByHash[checksum];
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
                    GlobalVariables.AudioManager.SongEnd += EndSong;
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

        public void ChangeSection()
        {
            if (!IsPractice)
            {
                return;
            }

            Paused = true;
            _pauseMenu.SetActive(false);
#if UNITY_EDITOR
            _debugText.gameObject.SetActive(false);
#endif
            PracticeManager.DisplayPracticeMenu();
        }

        public void SetSongTime(double time, double delayTime = SONG_START_DELAY)
        {
            double seekTime = time - delayTime;
            double inputTime = InputManager.CurrentInputTime;

            RealSongTime = seekTime;
            double inputOffset = InputManager.InputTimeOffset = inputTime
                - time - AudioCalibration // Offset backwards by the given time and by the audio calibration
                + delayTime;              // Bump forward by the delay so that times before the audio are negative

            Debug.Log(
                $"Set song time to {time}. Seek time: {seekTime}, input offset: {inputOffset}, input time: {inputTime}");

            // Audio seeking cannot go negative
            if (seekTime < 0) seekTime = 0;
            GlobalVariables.AudioManager.SetPosition(seekTime);
        }

        public void SetSongSpeed(float speed)
        {
            // 10% - 4995%, we reserve 5% so that audio syncing can still function
            speed = Math.Clamp(speed, 10 / 100f, 4995 / 100f);

            // Set speed; save old for input offset compensation
            float oldSpeed = SelectedSongSpeed;
            SelectedSongSpeed = speed;

            // Adjust input offset, otherwise input time will desync
            // TODO: This isn't 100% functional yet, changing speed quickly will still cause things to desync
            double oldOffset = InputManager.InputTimeOffset;
            double oldRelative = InputManager.RelativeUpdateTime;

            double oldBeforeSpeed = oldRelative * oldSpeed;
            double oldAfterSpeed = oldRelative * speed;
            double timeDifference = oldBeforeSpeed - oldAfterSpeed;

            double newOffset = InputManager.InputTimeOffset = oldOffset - timeDifference;
            double newRelative = InputManager.RelativeUpdateTime;
            double newBeforeSpeed = newRelative * oldSpeed;
            double newAfterSpeed = newRelative * speed;

            Debug.Log($"Set song speed to {speed:0.00}.\n"
                + $"Old input offset: {oldOffset:0.000000}, new: {newOffset:0.000000}, "
                + $"old input: {oldRelative:0.000000}, new: {newRelative:0.000000}, "
                + $"old w/old speed: {oldBeforeSpeed:0.000000}, old w/new: {oldAfterSpeed:0.000000}, "
                + $"new w/old speed: {newBeforeSpeed:0.000000}, new w/new: {newAfterSpeed:0.000000}, "
                + $"difference: {timeDifference:0.000000}");

            // Set based on the actual song speed, so as to not break resyncing
            GlobalVariables.AudioManager.SetSpeed(ActualSongSpeed);
        }

        public void AdjustSongSpeed(float deltaSpeed) => SetSongSpeed(SelectedSongSpeed + deltaSpeed);

        public void SetPaused(bool paused, bool timeCompensation = true)
        {
            Paused = paused;
            _pauseMenu.SetActive(paused);
#if UNITY_EDITOR
            _debugText.gameObject.SetActive(!paused);
#endif

            Debug.Log($"Paused: {paused}");

            if (paused)
            {
                _pauseStartTime = InputManager.CurrentInputTime;
                GlobalVariables.AudioManager.Pause();
            }
            else
            {
                if (timeCompensation)
                {
                    double totalPauseTime = InputManager.CurrentInputTime - _pauseStartTime;
                    InputManager.InputTimeOffset += totalPauseTime;
                }
                if (RealSongTime >= 0)
                {
                    GlobalVariables.AudioManager.Play();
                }
            }
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
            GlobalVariables.AudioManager.SongEnd -= EndSong;
            GlobalVariables.AudioManager.UnloadSong();

            GlobalVariables.Instance.IsReplay = false;
            GlobalVariables.Instance.LoadScene(SceneIndex.Menu);
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