using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using YARG.Core.Audio;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Core.Game;
using YARG.Core.Input;
using YARG.Core.Logging;
using YARG.Core.Replays;
using YARG.Core.Replays.Analyzer;
using YARG.Core.Song;
using YARG.Gameplay.HUD;
using YARG.Gameplay.Player;
using YARG.Input;
using YARG.Integration;
using YARG.Localization;
using YARG.Menu.Navigation;
using YARG.Menu.Persistent;
using YARG.Menu.ScoreScreen;
using YARG.Playback;
using YARG.Player;
using YARG.Replays;
using YARG.Scores;
using YARG.Settings;
using YARG.Venue.Characters;
using YARG.Venue.VenueCamera;

namespace YARG.Gameplay
{
    [DefaultExecutionOrder(-1)]
    public partial class GameManager : MonoBehaviour
    {
        public const double SONG_START_DELAY = SongRunner.SONG_START_DELAY;
        public const double SONG_END_DELAY = SONG_START_DELAY;

        public const double PAUSE_REWIND_LENGTH   = 1;
        public const double MAXIMUM_REWIND_TIME   = 3;
        public const double MAXIMUM_REWIND_WINDOW = 20;

        public const float TRACK_SPACING_X = 100f;

        public bool IsSeekingReplay;

        [Header("References")]
        [SerializeField]
        private TrackViewManager _trackViewManager;
        [SerializeField]
        private ReplayController _replayController;
        [SerializeField]
        private PauseMenuManager _pauseMenu;
        [SerializeField]
        private DraggableHudManager _draggableHud;

        [SerializeField]
        private GameObject _lyricBar;

        [SerializeField]
        private FailMeter _failMeter;

        [field: SerializeField]
        public VocalTrack VocalTrack { get; private set; }

        /// <summary>
        /// Equal to either <see cref="PlayerContainer.Players"/> or the players in the replay.
        /// </summary>
        public IReadOnlyList<YargPlayer> YargPlayers { get; private set;}

        private List<BasePlayer> _players;

        public int TotalPlayers => _players.Count;

        public bool IsSongStarted { get; private set; } = false;

        private SongRunner _songRunner;

        /// <remarks>
        /// This is not initialized on awake, but rather, in
        /// <see cref="GameplayBehaviour.OnChartLoaded"/>.
        /// </remarks>
        public BeatEventHandler BeatEventHandler { get;    private set; }
        public CrowdEventHandler CrowdEventHandler  { get; private set; }
        public CameraManager     VenueCameraManager { get; private set; }
        public CharacterManager  VenueCharacterManager { get; private set; }

        public PracticeManager  PracticeManager  { get; private set; }
        public BackgroundManager BackgroundManager { get; private set; }
        public EngineManager EngineManager { get; private set; }

        public SongEntry Song  { get; private set; }
        public SongChart    Chart { get; private set; }

        // For clarity, try to avoid using these properties inside GameManager itself
        // These are just to expose properties from the song runner to the outside
        /// <inheritdoc cref="SongRunner.SongTime"/>
        public double SongTime => _songRunner.SongTime;

        /// <inheritdoc cref="SongRunner.AudioTime"/>
        public double AudioTime => _songRunner.AudioTime;

        /// <inheritdoc cref="SongRunner.VisualTime"/>
        public double VisualTime => _songRunner.VisualTime;

        /// <inheritdoc cref="SongRunner.InputTime"/>
        public double InputTime => _songRunner.InputTime;

        /// <inheritdoc cref="SongRunner.SongSpeed"/>
        public float SongSpeed => _songRunner.SongSpeed;

        /// <inheritdoc cref="SongRunner.Started"/>
        public bool Started => _songRunner.Started;

        /// <inheritdoc cref="SongRunner.Paused"/>
        public bool Paused => _songRunner.Paused;

        /// <summary>
        /// Set when we are in the middle of resuming, but have not yet fully resumed
        /// </summary>
        public bool Rewinding { get; private set; }

        public double SongLength { get; private set; }

        public bool IsPractice      { get; private set; }

        public int BandScore
        {
            get => EngineManager.Score;
            set => EngineManager.Score = value;
        }

        public int BandCombo
        {
            get => EngineManager.Combo;
            set => EngineManager.Combo = value;
        }

        public float BandStars
        {
            get => EngineManager.Stars;
            set => EngineManager.Stars = value;
        }

        public int BandMultiplier => EngineManager.BandMultiplier;

        public double FirstNoteTime { get; private set; }
        public double LastNoteTime  { get; private set; }

        public ReplayInfo ReplayInfo { get; private set; }
        public ReplayData ReplayData { get; private set; }

        public List<PauseInfo> PauseInfo { get; } = new List<PauseInfo>();

        public IReadOnlyList<BasePlayer> Players => _players;

        public int StarPowerActivations { get; private set; } = 0;

        private bool _isReplaySaved;

        private int _originalSleepTimeout;

        private StemMixer _mixer;

        private List<double> _frameTimes;

        private double _pauseTime;

        public bool PlayingAShow => GlobalVariables.State.PlayingAShow;
        public int  ShowIndex = 0;

        private BandComboType _bandComboType;

        private        bool HasBots            => _players.Any(p => !p.Player.SittingOut && p.Player.Profile.IsBot);
        private static bool SaveScoresWithBots => SettingsManager.Settings.SaveScoresWithBots.Value;

        private void Awake()
        {
            // Set references
            PracticeManager = GetComponent<PracticeManager>();
            BackgroundManager = GetComponent<BackgroundManager>();
            EngineManager = new EngineManager();

            YargPlayers = PlayerContainer.Players;

            Song = GlobalVariables.State.CurrentSong;
            ReplayInfo = GlobalVariables.State.CurrentReplay;
            IsPractice = GlobalVariables.State.IsPractice && ReplayInfo == null;
            _bandComboType = SettingsManager.Settings.BandComboTypeSetting.Value;

            Navigator.Instance.PopAllSchemes();
            GameStateFetcher.SetSongEntry(Song);

            if (Song is null)
            {
                YargLogger.LogError("Null song set when loading gameplay!");

                GlobalVariables.Instance.LoadScene(SceneIndex.Menu);
                return;
            }

            // Hide vocals track (will be shown when players are initialized)
            VocalTrack.gameObject.SetActive(false);

            // Prevent screen from sleeping
            _originalSleepTimeout = Screen.sleepTimeout;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            // Update countdown display style from global settings
            CountdownDisplay.DisplayStyle = SettingsManager.Settings.CountdownDisplay.Value;

            _frameTimes = new List<double>();
        }

        private void OnDestroy()
        {
            YargLogger.LogInfo("Exiting song");

            if (Navigator.Instance != null)
            {
                Navigator.Instance.NavigationEvent -= OnNavigationEvent;
            }

            // Unsubscribe from other events
            SettingsManager.Settings.NoFailMode.OnChange -= OnNoFailModeChanged;
            SettingsManager.Settings.AutoCalibration.OnChange -= OnAutoCalibrationChanged;
            EngineManager.OnSongFailed -= OnSongFailed;

            //Restore stem volumes to their original state
            foreach (var (stem, state) in _stemStates)
            {
                GlobalAudioHandler.SetVolumeSetting(stem, state.Volume);
            }

            DisposeDebug();
            _pauseMenu.PopAllMenus();
            _mixer?.Dispose();
            _songRunner?.Dispose();
            BackgroundManager.Dispose();
            CrowdEventHandler.Dispose();

            // Reset the time scale back, as it would be 0 at this point (because of pausing)
            Time.timeScale = 1f;

            // Reset sleep timeout setting
            Screen.sleepTimeout = _originalSleepTimeout;
        }

        private void Update()
        {
            // Pause/unpause
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (_draggableHud.EditMode)
                {
                    SetEditHUD(false);
                }

                if ((!IsPractice || PracticeManager.HasSelectedSection) &&
                    !DialogManager.Instance.IsDialogShowing &&
                    !PlayerHasFailed)
                {
                    SetPaused(!_pauseMenu.IsOpen);
                }
            }

            // Toggle debug text
            if (Keyboard.current.ctrlKey.isPressed && Keyboard.current.tabKey.wasPressedThisFrame)
            {
                ToggleDebugEnabled();
            }

            // Skip the rest if paused
            if (_songRunner.Paused)
            {
                return;
            }

            // Update handlers
            _songRunner.Update();
            BeatEventHandler.Update(_songRunner.SongTime, _songRunner.VisualTime);
            CrowdEventHandler.Update(_songRunner.SongTime);

            // Update players
            int totalScore = 0;
            float totalStars = 0f;
            foreach (var player in _players)
            {
                player.GameplayUpdate();

                totalScore += player.Score;
                totalScore += player.BandBonusScore;
                totalStars += player.Stars;
            }

            if (GlobalVariables.VerboseReplays)
            {
                _frameTimes.Add(_songRunner.InputTime);
            }

            BandScore = totalScore;
            BandStars = totalStars / _players.Count;

            // End song if needed (required for the [end] event)
            if (_songRunner.SongTime >= SongLength)
            {
                if (EndSong())
                {
                    return;
                }
            }
        }

        public void SetSongTime(double time, double delayTime = SONG_START_DELAY)
        {
            _songRunner.SetSongTime(time, delayTime);

            BeatEventHandler.Reset();
            BackgroundManager.SetTime(_songRunner.SongTime + Song.SongOffsetSeconds);
            VenueCameraManager?.ResetTime(time);
            VenueCharacterManager?.ResetTime(time);
        }

        public void SetSongSpeed(float speed)
        {
            _songRunner.SetSongSpeed(speed);

            BackgroundManager.SetSpeed(_songRunner.SongSpeed);
        }

        public int GetMixerFFTData(float[] buffer, int fftSize, bool complex)
        {
            return _mixer.GetFFTData(buffer, fftSize, complex);
        }

        public int GetMixerSampleData(float[] buffer)
        {
            return _mixer.GetSampleData(buffer);
        }

        public void AdjustSongSpeed(float deltaSpeed)
        {
            _songRunner.AdjustSongSpeed(deltaSpeed);

            // Only scale the player speed in practice
            if (IsPractice && _songRunner.SongSpeed >= 1)
            {
                // Scale only if the speed is greater than 1
                var speed = _songRunner.SongSpeed >= 1 ? _songRunner.SongSpeed : 1;
                foreach (var player in _players)
                {
                    player.BaseEngine.SetSpeed(speed);
                }
            }

            BackgroundManager.SetSpeed(_songRunner.SongSpeed);
        }

        public void Pause(bool showMenu = true)
        {
            _songRunner.Pause();
            PauseCore(showMenu);
        }

        private void PauseCore(bool showMenu)
        {
            if (showMenu)
            {
                if (!GlobalVariables.State.PlayingWithReplay && ReplayInfo != null)
                {
                    _pauseMenu.PushMenu(PauseMenuManager.Menu.ReplayPause);
                }
                else if (PlayerHasFailed)
                {
                    _pauseMenu.PushMenu(PauseMenuManager.Menu.FailPause);
                }
                else if (IsPractice)
                {
                    _pauseMenu.PushMenu(PauseMenuManager.Menu.PracticePause);
                }
                else if (GlobalVariables.State.PlayingAShow)
                {
                    _pauseMenu.PushMenu(PauseMenuManager.Menu.SetlistPause);
                }
                else
                {
                    _pauseMenu.PushMenu(PauseMenuManager.Menu.QuickPlayPause);
                }
            }

            // Pause the background/venue
            Time.timeScale = 0f;
            BackgroundManager.SetPaused(true);
            GameStateFetcher.SetPaused(true);

            // Save state about the pause

            // This uses the raw input update time because it keeps running during the pause
            // allowing us to accurately calculate the length of the pause later
            _pauseTime = InputManager.InputUpdateTime;
            var pauseInfo = new PauseInfo
            {
                PauseTime = SongTime,
                PauseLength = 0
            };
            PauseInfo.Add(pauseInfo);

            // Pause any audio samples that are currently playing
            GlobalAudioHandler.PauseAllSfx();

            // Allow sleeping
            Screen.sleepTimeout = _originalSleepTimeout;
        }

        public bool PlayerHasFailed { get; set; } = false;

        public async void Resume()
        {
            Rewinding = true;
            // Update the last PauseInfo with the pause
            var currentPause = PauseInfo[^1];
            currentPause.PauseLength = InputManager.InputUpdateTime - _pauseTime;
            PauseInfo[^1] = currentPause;

            _pauseMenu.PopAllMenus();
            Time.timeScale = 1f;
            await RewindAndResume(PAUSE_REWIND_LENGTH);

            // _songRunner.Resume();
            ResumeCore();
        }

        public void UpdateCalibration()
        {
            _songRunner.UpdateCalibration();
        }

        public void ResumeCore()
        {
            if (_draggableHud.EditMode)
            {
                SetEditHUD(false);
            }

            if (!Rewinding)
            {
                _pauseMenu.PopAllMenus();
            }

            if (_songRunner.SongTime >= SongLength + SONG_END_DELAY)
            {
                return;
            }

            // Unpause the background/venue
            Time.timeScale = 1f;
            BackgroundManager.SetPaused(false);
            GameStateFetcher.SetPaused(false);

            // Unpause any audio samples that are currently playing
            GlobalAudioHandler.ResumeAllSfx();

            // Disallow sleeping
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            _isReplaySaved = false;

            Rewinding = false;

            foreach (var player in _players)
            {
                player.SendInputsOnResume();
            }
        }

        public void SetPaused(bool paused)
        {
            // Does not delegate out to _songRunner.SetPaused since we need extra logic
            if (paused)
            {
                Pause();
            }
            else
            {
                Resume();
            }
        }

        public void OverridePause()
        {
            _songRunner.OverridePause();
            PauseCore(showMenu: false);
        }

        public bool OverrideResume()
        {
            bool resumed = _songRunner.OverrideResume();
            if (resumed)
            {
                ResumeCore();
            }

            return resumed;
        }

        public double GetRelativeInputTime(double timeFromInputSystem)
            => _songRunner.GetRelativeInputTime(timeFromInputSystem);

        private bool EndSong()
        {
            if (IsPractice)
            {
                PracticeManager.ResetPractice();
                return false;
            }

            if (_songRunner.SongTime < SongLength + SONG_END_DELAY)
            {
                return false;
            }

            if (!GlobalVariables.State.PlayingWithReplay && ReplayInfo != null)
            {
                Pause(false);
                return true;
            }
#nullable enable
            ReplayInfo? replayInfo = null;
#nullable disable
            try
            {
                _isReplaySaved = false;
                replayInfo = SaveReplay(_songRunner.InputTime, ScoreContainer.ScoreReplayDirectory);
            }
            catch (Exception e)
            {
                YargLogger.LogException(e, "Failed to save replay!");
            }

            // Pass the score info to the stats screen
            GlobalVariables.State.ScoreScreenStats = new ScoreScreenStats
            {
                PlayerScores = _players.Select(player => new PlayerScoreCard
                {
                    IsHighScore = player.Score > player.LastHighScore,
                    Player = player.Player,
                    Stats = player.BaseStats
                }).ToArray(),
                BandScore = BandScore,
                BandStars = (int) BandStars,
                ReplayInfo = replayInfo,
            };

            RecordScores(replayInfo);

            // Dispose the crowd handler
            CrowdEventHandler.Dispose();

            // Go to the score screen
            GlobalVariables.Instance.LoadScene(SceneIndex.Score);
            return true;
        }

        private void RecordScores(ReplayInfo replayInfo)
        {
            if (!ScoreContainer.IsBandScoreValid(SongSpeed))
            {
                return;
            }

            // Get all of the individual player score entries
            var playerEntries = new List<PlayerScoreRecord>();

            foreach (var player in _players)
            {
                var profile = player.Player.Profile;

                // Skip bots and anyone that's obviously cheating.
                if (!ScoreContainer.IsSoloScoreValid(SongSpeed, player.Player))
                {
                    continue;
                }

                playerEntries.Add(new PlayerScoreRecord
                {
                    PlayerId = profile.Id,

                    Instrument = profile.CurrentInstrument,
                    Difficulty = profile.CurrentDifficulty,

                    EnginePresetId = profile.EnginePreset,

                    Score = player.Score,
                    Stars = StarAmountHelper.GetStarsFromInt((int) player.Stars),

                    NotesHit = player.BaseStats.NotesHit,
                    NotesMissed = player.BaseStats.NotesMissed,
                    IsFc = player.IsFc,
                    IsReplay = player.Player.IsReplay,

                    Percent = player.BaseStats.Percent
                });
            }

            var validScoreCount = _players.Count(p => ScoreContainer.IsSoloScoreValid(SongSpeed, p.Player));
            if (validScoreCount == 0)
            {
                return;
            }

            int humanBandScore = 0;
            float humanBandStars = 0f;
            if (HasBots && SaveScoresWithBots)
            {
                // Simulate the replay with only human players to calculate the correct score.
                // This will remove band multiplier and Star Power contribution from bots
                if (replayInfo == null || ReplayData == null)
                {
                    return;
                }

                var results = ReplayAnalyzer.AnalyzeReplay(Chart, replayInfo, ReplayData);
                foreach (var result in results)
                {
                    humanBandScore += result.ResultStats.TotalScore + result.ResultStats.BandBonusScore;
                    humanBandStars += result.ResultStats.Stars;
                }
            }
            else
            {
                // No bots, use live scores directly
                foreach (var player in _players)
                {
                    humanBandScore += player.Score + player.BaseStats.BandBonusScore;
                    humanBandStars += player.Stars;
                }
            }

            // Calculate band stars by taking average stars for human players only
            int humanCount = playerEntries.Count;
            int averageStars = (int)(humanBandStars / humanCount);
            var bandStars = humanCount > 0
                ? StarAmountHelper.GetStarsFromInt(averageStars)
                : StarAmount.None;

            ScoreContainer.RecordScore(new GameRecord
            {
                Date = DateTime.Now,

                SongChecksum = Song.Hash.HashBytes,
                SongName = Song.Name,
                SongArtist = Song.Artist,
                SongCharter = Song.Charter,

                ReplayFileName = replayInfo?.ReplayName,
                ReplayChecksum = replayInfo?.ReplayChecksum.HashBytes,

                BandScore = humanBandScore,
                BandStars = bandStars,

                SongSpeed = SongSpeed,
                PlayedWithReplay = GlobalVariables.State.PlayingWithReplay,
                HasBots = HasBots,
            }, playerEntries);
        }

        public void ForceQuitSong()
        {
            GlobalVariables.State = PersistentState.Default;
            GlobalVariables.Instance.LoadScene(SceneIndex.Menu);
        }

        public void SetVenueCameraManager(CameraManager cameraManager)
        {
            VenueCameraManager = cameraManager;
        }

        public void SetVenueCharacterManager(CharacterManager characterManager)
        {
            VenueCharacterManager = characterManager;
        }

        public void SetEditHUD(bool on)
        {
            if (on)
            {
                _pauseMenu.gameObject.SetActive(false);
                _draggableHud.SetEditHUD(true);
            }
            else
            {
                _draggableHud.SetEditHUD(false);
                _pauseMenu.gameObject.SetActive(true);
            }
        }

#nullable enable
        public ReplayInfo? SaveReplay(double length, string directory)
#nullable disable
        {
            if (_isReplaySaved)
            {
                return null;
            }

            var frames = new List<ReplayFrame>(_players.Count);
            var replayStats = new List<ReplayStats>(_players.Count);
            var colorProfiles = new Dictionary<Guid, ColorProfile>();
            var cameraPresets = new Dictionary<Guid, CameraPreset>();

            int bandScore = 0;
            float bandStars = 0f;
            for (int i = 0; i < _players.Count; i++)
            {
                var player = _players[i];
                if (player.Player.Profile.IsBot)
                {
                    continue;
                }

                var (frame, stats) = player.ConstructReplayData();
                frames.Add(frame);
                replayStats.Add(stats);
                bandScore += player.Score;
                bandStars += player.Stars;

                if (!player.Player.ColorProfile.DefaultPreset)
                {
                    colorProfiles.TryAdd(player.Player.ColorProfile.Id, player.Player.ColorProfile);
                }

                if (!player.Player.CameraPreset.DefaultPreset)
                {
                    cameraPresets.TryAdd(player.Player.CameraPreset.Id, player.Player.CameraPreset);
                }
            }

            if (frames.Count == 0)
            {
                return null;
            }

            var stars = StarAmountHelper.GetStarsFromInt((int) (bandStars / frames.Count));
            ReplayData = new ReplayData(colorProfiles, cameraPresets, frames.ToArray(), _frameTimes.ToArray());

            (bool success, var replayInfo) = ReplayIO.TrySerialize(directory, Song, SongSpeed, length, bandScore, stars, PauseInfo.ToArray(), replayStats.ToArray(), ReplayData);
            if (!success)
            {
                return null;
            }

            ReplayContainer.AddEntry(replayInfo);
            _isReplaySaved = true;
            return replayInfo;
        }

        private void OnNavigationEvent(NavigationContext context)
        {
            switch (context.Action)
            {
                // Pause
                case MenuAction.Start:
                    if (_draggableHud.EditMode)
                    {
                        SetEditHUD(false);
                    }

                    if ((!IsPractice || PracticeManager.HasSelectedSection) && !DialogManager.Instance.IsDialogShowing && !PlayerHasFailed)
                    {
                        SetPaused(!_songRunner.Paused);
                    }
                    break;
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && !Paused && SettingsManager.Settings.PauseOnFocusLoss.Value)
            {
                SetPaused(true);
            }
        }

        public void ResetBandCombo()
        {
            switch (_bandComboType)
            {
                case BandComboType.Strict:
                    BandCombo = 0;
                break;
                case BandComboType.Lenient:
                    BandCombo = Players.Sum(e => e.Combo * e.BaseStats.BandComboUnits);
                break;
            }
        }

        public void AddBandCombo(int amount)
        {
            BandCombo += amount;
        }

        private async void OnSongFailed()
        {
            if (SettingsManager.Settings.NoFailMode.Value || IsPractice)
            {
                return;
            }

            if (!PlayerHasFailed)
            {
                PlayerHasFailed = true;
                _mixer.FadeOut(SONG_END_DELAY);
                await UniTask.Delay(TimeSpan.FromSeconds(SONG_END_DELAY));
                GlobalAudioHandler.PlayVoxSample(VoxSample.FailSound);
                Pause();
            }
        }

        private void OnAutoCalibrationChanged(bool enabled)
        {
            if (enabled)
            {
                InvalidateScores("Menu.Toast.AutoCalibrationScore");
            }
        }

        // If we go from no fail to fail, we need to reinitialize the happiness state so we avoid
        // the possibility of an instant fail. Yes, this is cheeseable since toggling no fail resets happiness.
        private void OnNoFailModeChanged(bool noFail)
        {
            // If we're going from no fail to fail and happiness would result in an insta-fail, reset happiness,
            // but also inhibit score saving to avoid cheesing
            if (!noFail && EngineManager.Happiness <= 0f)
            {
                InvalidateScores("Menu.Toast.NoFailScore");

                EngineManager.InitializeHappiness();
            }
        }

        private void InvalidateScores(string toastKey)
        {
            bool invalidated = false;

            foreach (var player in _players)
            {
                if (player.Player.IsScoreValid)
                {
                    invalidated = true;
                }

                player.Player.IsScoreValid = false;
            }

            if (invalidated && !string.IsNullOrEmpty(toastKey))
            {
                ToastManager.ToastWarning(Localize.Key(toastKey));
            }
        }

        private void CheckForRewindInvalidation()
        {
            if (PauseInfo.Count == 0)
            {
                return;
            }

            // If there is more than MAXIMUM_REWIND_TIME seconds of rewind in MAXIMUM_REWIND_WINDOW of song time, invalidate scores
            var start = 0;

            for (var end = 0; end < PauseInfo.Count; end++)
            {
                var endTime = PauseInfo[end].PauseTime;

                while (PauseInfo[start].PauseTime < endTime - MAXIMUM_REWIND_WINDOW)
                {
                    start++;
                }

                var pauses = end - start + 1;

                if (pauses * PAUSE_REWIND_LENGTH > MAXIMUM_REWIND_TIME)
                {
                    InvalidateScores("Menu.Toast.TooManyPauses");
                    return;
                }
            }
        }

        private async UniTask RewindAndResume(double seconds)
        {
            YargLogger.LogFormatDebug("Rewinding {0} seconds at VisualTime {1}", seconds, VisualTime);
            // First we have to set timeScale back to 1 and rewind VisualTime by seconds over a quarter or half second
            // Then we have to seek audio back by seconds and save InputManager.InputUpdateTime;
            // Then, when InputManager.InputUpdateTime reaches the saved resume time plus seconds, we can resume songrunner

            // Rewind players
            foreach (var player in _players)
            {
                player.Rewind(VisualTime - seconds);
            }

            await _songRunner.RewindAndResume(seconds);

            foreach (var player in _players)
            {
                player.PostRewind(VisualTime - seconds);
            }

            CheckForRewindInvalidation();
        }
    }
}
