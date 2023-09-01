using System;
using System.Collections.Generic;
using System.IO;
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
using YARG.Integration;
using YARG.Menu.Navigation;
using YARG.Menu.Persistent;
using YARG.Player;
using YARG.Replays;

namespace YARG.Gameplay
{
    public partial class GameManager : MonoBehaviour
    {
        [Header("References")]

        [SerializeField]
        private TrackViewManager _trackViewManager;

        [SerializeField]
        private PauseMenuManager _pauseMenu;

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

        private double _pauseStartTime;

        private IReadOnlyList<YargPlayer> _yargPlayers;
        private List<BasePlayer> _players;

        public bool IsSongStarted { get; private set; } = false;

        private bool _loadFailure;
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
                if (chart != null)
                    value?.Invoke(chart);
            }
            remove => _chartLoaded -= value;
        }

        private event Action _songStarted;
        public event Action SongStarted
        {
            add
            {
                _songStarted += value;

                // Invoke now if already loaded, this event is only fired once
                if (IsSongStarted)
                    value?.Invoke();
            }
            remove => _songStarted -= value;
        }

        public PracticeManager PracticeManager { get; private set; }
        public BeatEventManager BeatEventManager { get; private set; }

        public SongMetadata Song { get; private set; }
        public SongChart Chart { get; private set; }

        public float SelectedSongSpeed { get; private set; }
        public float ActualSongSpeed => SelectedSongSpeed + _syncSpeedAdjustment;

        public double SongLength { get; private set; }

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
            // Set references
            PracticeManager = GetComponent<PracticeManager>();
            BeatEventManager = GetComponent<BeatEventManager>();

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
            {
                LoadingManager.Instance.Queue(LoadReplay, "Loading replay...");
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

            // Loaded, enable updates
            enabled = true;
            IsSongStarted = true;
            _songStarted?.Invoke();
        }

        private void Update()
        {
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (IsPractice && !PracticeManager.HasSelectedSection)
                {
                    return;
                }

                SetPaused(!Paused);
            }

            if (Paused)
            {
                return;
            }

            UpdateTimes();

            if (Keyboard.current.ctrlKey.isPressed && Keyboard.current.tabKey.wasPressedThisFrame)
            {
                _isShowDebugText = !_isShowDebugText;

                _debugText.gameObject.SetActive(_isShowDebugText);
            }

            if (_isShowDebugText)
            {
                if (_players[0] is FiveFretPlayer fiveFretPlayer)
                {
                    byte buttonMask = fiveFretPlayer.Engine.State.ButtonMask;
                    int noteIndex = fiveFretPlayer.Engine.State.NoteIndex;
                    var ticksPerEight = fiveFretPlayer.Engine.State.TicksEveryEightMeasures;
                    double starPower = fiveFretPlayer.Engine.EngineStats.StarPowerAmount;

                    _debugText.text =
                        $"Note index: {noteIndex}\n" +
                        $"Buttons: {buttonMask}\n" +
                        $"Star Power: {starPower:0.0000}\n" +
                        $"TicksPerEight: {ticksPerEight}\n";

                }
                else if (_players[0] is DrumsPlayer drumsPlayer)
                {
                    int noteIndex = drumsPlayer.Engine.State.NoteIndex;

                    _debugText.text =
                        $"Note index: {noteIndex}\n";
                }

                _debugText.text +=
                    $"Input time: {InputTime:0.000000}\n" +
                    $"Song time: {SongTime:0.000000}\n" +
                    $"Time difference: {InputTime - SongTime:0.000000}\n" +
                    $"Speed adjustment: {_syncSpeedAdjustment:0.00}\n" +
                    $"Speed multiplier: {_syncSpeedMultiplier}\n" +
                    $"Sync start delta: {_syncStartDelta:0.000000}";
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

            Song = GlobalVariables.Instance.SongContainer.SongsByHash[GlobalVariables.Instance.CurrentReplay.SongChecksum][0];
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
                yargPlayer.ColorProfile = Replay.ColorProfiles[frame.PlayerInfo.ColorProfileId];

                players.Add(new YargPlayer(frame.PlayerInfo.Profile, null, false));
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
                if (Chart.Sections.Count > 0) {
                    uint lastTick = Chart.GetLastTick();
                    Chart.Sections[^1].TickLength = lastTick;
                    Chart.Sections[^1].TimeLength = Chart.SyncTrack.TickToTime(lastTick);
                }
            });

            if (_loadFailure)
                return;

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
                    IAudioManager.LoadAudio(GlobalVariables.AudioManager, Song, SelectedSongSpeed);
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
                    GameMode.FiveFretGuitar => _fiveFretGuitarPrefab,
                    GameMode.SixFretGuitar  => _sixFretGuitarPrefab,
                    GameMode.FourLaneDrums  => _fourLaneDrumsPrefab,
                    GameMode.FiveLaneDrums  => _fiveLaneDrumsPrefab,
                    GameMode.ProGuitar      => _proGuitarPrefab,

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
                basePlayer.Initialize(index, player, Chart, trackView);
                _players.Add(basePlayer);
            }
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

#if UNITY_EDITOR
            Debug.Log($"Set song speed to {speed:0.00}.\n"
               + $"Input time: {InputTime:0.000000}, song time: {SongTime:0.000000}");
#endif
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

            _pauseStartTime = RealInputTime;
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

            if (RealSongTime >= SongOffset)
                GlobalVariables.AudioManager.Play();
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

        private void EndSong()
        {
            if (IsPractice)
            {
                PracticeManager.ResetPractice();
            }
            if (!IsReplay)
            {
                var replay = ReplayContainer.CreateNewReplay(Song, _players);
                var entry = ReplayContainer.CreateEntryFromReplayFile(new ReplayFile(replay));

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