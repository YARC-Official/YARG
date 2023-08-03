using System;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using YARG.Core;
using YARG.Core.Chart;
using YARG.Core.Game;
using YARG.Core.Replays;
using YARG.Core.Replays.IO;
using YARG.Gameplay.HUD;
using YARG.Gameplay.Player;
using YARG.Input;
using YARG.Player;
using YARG.Replays;
using YARG.Settings;
using YARG.Song;

namespace YARG.Gameplay
{
    public class GameManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private TrackViewManager _trackViewManager;

        [SerializeField]
        private PracticeSectionMenu _practiceSectionMenu;

        [SerializeField]
        private GameObject _pauseMenu;

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

        private const float SONG_START_DELAY = 2f;

        // All access to chart data must be done through this event,
        // since things are loaded asynchronously
        // Players are initialized by hand and don't go through this event
        public event Action<SongChart> ChartLoaded;

        public SongEntry Song { get; private set; }

        public float SelectedSongSpeed { get; private set; }
        public float ActualSongSpeed   { get; private set; }

        public double SongStartTime { get; private set; }
        public double SongLength    { get; private set; }

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
        // Remember that calibration is already accounted for by "relative update time"
        public double InputTime => InputManager.RelativeUpdateTime * SelectedSongSpeed;

        /// <summary>
        /// The current input update <b>at this instant</b>, accounting for song speed, <b>and for</b> calibration.
        /// </summary>
        public double InstantInputTime => InputManager.RelativeUpdateTime * SelectedSongSpeed;

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

        private double _pauseStartTime;

        private SongChart _chart;

        private IReadOnlyList<YargPlayer> _yargPlayers;

        private List<BasePlayer> _players;
        private List<Beatline>   _beats;

        public IReadOnlyList<BasePlayer> Players => _players;

        private UniTask _syncAudio = UniTask.CompletedTask;

        private void Awake()
        {
            Song = GlobalVariables.Instance.CurrentSong;
            IsReplay = GlobalVariables.Instance.IsReplay;
            IsPractice = GlobalVariables.Instance.IsPractice;
            SelectedSongSpeed = GlobalVariables.Instance.SongSpeed;
            ActualSongSpeed = SelectedSongSpeed;

            _yargPlayers = PlayerContainer.Players;

            if (IsReplay)
            {
                string checksum = GlobalVariables.Instance.CurrentReplay.SongChecksum;
                var result = ReplayContainer.LoadReplayFile(GlobalVariables.Instance.CurrentReplay, out var replay);

                Song = SongContainer.SongsByHash[checksum];
                if (Song is null || result != ReplayReadResult.Valid)
                {
                    GlobalVariables.Instance.LoadScene(SceneIndex.Menu);
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

            if (IsPractice)
            {
                // Don't start until a section has been selected
                Paused = true;
                _practiceSectionMenu.gameObject.SetActive(true);
            }
        }

        private async UniTask Start()
        {
            // Disable until everything's loaded
            enabled = false;

            // Load chart/audio
            LoadingManager.Instance.Queue(LoadChart, "Loading chart...");
            LoadingManager.Instance.Queue(LoadAudio, "Loading audio...");
            await LoadingManager.Instance.StartLoad();

            // Spawn players
            CreatePlayers();

            // Set start time
            SetSongTime(0);

            // Loaded, enable updates
            enabled = true;
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

            float speedAdjustment = delta > 0.0 ? SPEED_ADJUSTMENT : -SPEED_ADJUSTMENT;
            ActualSongSpeed = SelectedSongSpeed + speedAdjustment;
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
            ActualSongSpeed = SelectedSongSpeed;
            GlobalVariables.AudioManager.SetSpeed(ActualSongSpeed);

            inputTime = RealInstantInputTime;
            audioTime = GlobalVariables.AudioManager.CurrentPositionD;
            double finalDelta = inputTime - audioTime;
            Debug.Log($"Audio synced. Input: {inputTime}, audio: {audioTime}, delta: {finalDelta}");
        }

        private async UniTask LoadChart()
        {
            await UniTask.RunOnThreadPool(() =>
            {
                string notesFile = Path.Combine(Song.Location, Song.NotesFile);
                Debug.Log(notesFile);
                _chart = SongChart.FromFile(new SongMetadata(), notesFile);

                var syncTrack = _chart.SyncTrack;
                if (syncTrack.Beatlines is null or { Count: < 1 })
                    _chart.SyncTrack.GenerateBeatlines(_chart.GetLastTick());

                _beats = _chart.SyncTrack.Beatlines;
            });

            ChartLoaded?.Invoke(_chart);
        }

        private async UniTask LoadAudio()
        {
            await UniTask.RunOnThreadPool(() =>
            {
                Song.LoadAudio(GlobalVariables.AudioManager, SelectedSongSpeed);
                SongLength = GlobalVariables.AudioManager.AudioLengthD;
                GlobalVariables.AudioManager.SongEnd += EndSong;
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

        public void SetPracticeSection(Section start, Section end)
        {
            foreach (var player in Players)
            {
                player.SetPracticeSection(start, end);
            }

            Debug.Log($"{start.Name} ({start.Time}) - {end.Name} ({end.Time})");

            SetSongTime(start.Time - SONG_START_DELAY);
        }

        private void SetSongTime(double time, double delayTime = SONG_START_DELAY)
        {
            double seekTime = time - delayTime;

            RealSongTime = seekTime;
            InputManager.InputTimeOffset = InputManager.CurrentInputTime
                - time - AudioCalibration // Offset backwards by the given time and by the audio calibration
                + SONG_START_DELAY;       // Bump forward by the delay so that times before the audio are negative

            // Audio seeking cannot go negative
            if (seekTime < 0)
                seekTime = 0;
            GlobalVariables.AudioManager.SetPosition(seekTime);
        }

        public void SetPaused(bool paused)
        {
            Paused = paused;
            _pauseMenu.SetActive(paused);

            Debug.Log($"Paused: {paused}");

            if (paused)
            {
                _pauseStartTime = InputManager.CurrentInputTime;
                GlobalVariables.AudioManager.Pause();
            }
            else
            {
                double totalPauseTime = InputManager.CurrentInputTime - _pauseStartTime;
                InputManager.InputTimeOffset += totalPauseTime;
                GlobalVariables.AudioManager.Play();
            }
        }

        private void EndSong()
        {
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
    }
}