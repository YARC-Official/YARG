using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Cysharp.Text;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using YARG.Audio;
using YARG.Core;
using YARG.Core.Audio;
using YARG.Core.Chart;
using YARG.Core.Engine.Drums;
using YARG.Core.Engine.Guitar;
using YARG.Core.Engine.Vocals;
using YARG.Core.Game;
using YARG.Core.Input;
using YARG.Core.Logging;
using YARG.Core.Replays;
using YARG.Core.Song;
using YARG.Gameplay.HUD;
using YARG.Gameplay.Player;
using YARG.Helpers;
using YARG.Helpers.Extensions;
using YARG.Integration;
using YARG.Menu.Navigation;
using YARG.Menu.Persistent;
using YARG.Menu.ScoreScreen;
using YARG.Playback;
using YARG.Player;
using YARG.Replays;
using YARG.Scores;
using YARG.Settings;

namespace YARG.Gameplay
{
    [DefaultExecutionOrder(-1)]
    public partial class GameManager : MonoBehaviour
    {
        public const double SONG_START_DELAY = SongRunner.SONG_START_DELAY;
        public const double SONG_END_DELAY = SONG_START_DELAY;

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

        [field: SerializeField]
        public VocalTrack VocalTrack { get; private set; }

        /// <summary>
        /// Equal to either <see cref="PlayerContainer.Players"/> or the players in the replay.
        /// </summary>
        public IReadOnlyList<YargPlayer> YargPlayers { get; private set;}

        private List<BasePlayer> _players;

        public bool IsSongStarted { get; private set; } = false;

        private SongRunner _songRunner;

        /// <remarks>
        /// This is not initialized on awake, but rather, in
        /// <see cref="GameplayBehaviour.OnChartLoaded"/>.
        /// </remarks>
        public BeatEventHandler BeatEventHandler { get; private set; }

        public PracticeManager  PracticeManager  { get; private set; }
        public BackgroundManager BackgroundManager { get; private set; }

        public SongEntry Song  { get; private set; }
        public SongChart    Chart { get; private set; }

        // For clarity, try to avoid using these properties inside GameManager itself
        // These are just to expose properties from the song runner to the outside
        /// <inheritdoc cref="SongRunner.SongTime"/>
        public double SongTime => _songRunner.SongTime;

        /// <inheritdoc cref="SongRunner.RealSongTime"/>
        public double RealSongTime => _songRunner.RealSongTime;

        /// <inheritdoc cref="SongRunner.AudioTime"/>
        public double AudioTime => _songRunner.AudioTime;

        /// <inheritdoc cref="SongRunner.RealAudioTime"/>
        public double RealAudioTime => _songRunner.RealAudioTime;

        /// <inheritdoc cref="SongRunner.VisualTime"/>
        public double VisualTime => _songRunner.VisualTime;

        /// <inheritdoc cref="SongRunner.RealVisualTime"/>
        public double RealVisualTime => _songRunner.RealVisualTime;

        /// <inheritdoc cref="SongRunner.InputTime"/>
        public double InputTime => _songRunner.InputTime;

        /// <inheritdoc cref="SongRunner.RealInputTime"/>
        public double RealInputTime => _songRunner.RealInputTime;

        /// <inheritdoc cref="SongRunner.SongSpeed"/>
        public float SongSpeed => _songRunner.SongSpeed;

        /// <inheritdoc cref="SongRunner.Started"/>
        public bool Started => _songRunner.Started;

        /// <inheritdoc cref="SongRunner.Paused"/>
        public bool Paused => _songRunner.Paused;

        public double SongLength { get; private set; }

        public bool IsPractice      { get; private set; }

        public int   BandScore { get; private set; }
        public int   BandCombo { get; private set; }
        public float BandStars { get; private set; }

        public ReplayInfo ReplayInfo { get; private set; }
        public ReplayData ReplayData { get; private set; }

        public IReadOnlyList<BasePlayer> Players => _players;

        private bool _isReplaySaved;

        private int _originalSleepTimeout;

        private StemMixer _mixer;

        private void Awake()
        {
            // Set references
            PracticeManager = GetComponent<PracticeManager>();
            BackgroundManager = GetComponent<BackgroundManager>();

            YargPlayers = PlayerContainer.Players;

            Song = GlobalVariables.State.CurrentSong;
            ReplayInfo = GlobalVariables.State.CurrentReplay;
            IsPractice = GlobalVariables.State.IsPractice && ReplayInfo == null;

            Navigator.Instance.PopAllSchemes();
            GameStateFetcher.SetSongEntry(Song);

            if (Song is null)
            {
                YargLogger.LogError("Null song set when loading gameplay!");

                GlobalVariables.Instance.LoadScene(SceneIndex.Menu);
                return;
            }

            // Hide vocals track (will be shown when players are initialized
            VocalTrack.gameObject.SetActive(false);

            // Prevent screen from sleeping
            _originalSleepTimeout = Screen.sleepTimeout;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            // Update countdown display style from global settings
            CountdownDisplay.DisplayStyle = SettingsManager.Settings.CountdownDisplay.Value;
        }

        private void OnDestroy()
        {
            YargLogger.LogInfo("Exiting song");

            if (Navigator.Instance != null)
            {
                Navigator.Instance.NavigationEvent -= OnNavigationEvent;
            }

            foreach (var state in _stemStates)
            {
                GlobalAudioHandler.SetVolumeSetting(state.Key, state.Value.Volume);
            }

            DisposeDebug();
            _pauseMenu.PopAllMenus();
            _mixer?.Dispose();
            _songRunner?.Dispose();
            BeatEventHandler?.Unsubscribe(StarPowerClap);
            BackgroundManager.Dispose();

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
                if ((!IsPractice || PracticeManager.HasSelectedSection) && !DialogManager.Instance.IsDialogShowing)
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
            BeatEventHandler.Update(_songRunner.SongTime);

            // Update players
            int totalScore = 0;
            int totalCombo = 0;
            float totalStars = 0f;
            foreach (var player in _players)
            {
                player.UpdateWithTimes(_songRunner.InputTime);

                totalScore += player.Score;
                totalCombo += player.Combo;
                totalStars += player.Stars;
            }

            BandScore = totalScore;
            BandCombo = totalCombo;
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

            BeatEventHandler.ResetTimers();
            BackgroundManager.SetTime(_songRunner.SongTime + Song.SongOffsetSeconds);
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
                if (ReplayInfo != null)
                {
                    _pauseMenu.PushMenu(PauseMenuManager.Menu.ReplayPause);
                }
                else if (IsPractice)
                {
                    _pauseMenu.PushMenu(PauseMenuManager.Menu.PracticePause);
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

            // Allow sleeping
            Screen.sleepTimeout = _originalSleepTimeout;
        }

        public void Resume()
        {
            _songRunner.Resume();
            ResumeCore();
        }

        public void ResumeCore()
        {
            if (_draggableHud.EditMode)
            {
                SetEditHUD(false);
            }

            _pauseMenu.PopAllMenus();
            if (_songRunner.SongTime >= SongLength + SONG_END_DELAY)
            {
                return;
            }

            // Unpause the background/venue
            Time.timeScale = 1f;
            BackgroundManager.SetPaused(false);
            GameStateFetcher.SetPaused(false);

            // Disallow sleeping
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            _isReplaySaved = false;

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

        public double GetCalibratedRelativeInputTime(double timeFromInputSystem)
            => _songRunner.GetCalibratedRelativeInputTime(timeFromInputSystem);

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

            if (ReplayInfo != null)
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
                replayInfo = SaveReplay(Song.SongLengthSeconds, ScoreContainer.ScoreReplayDirectory);
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

                    Percent = player.BaseStats.Percent
                });
            }

            // Record the score into the database (but only if there are no bots, and Song Speed is at least 100%)
            ScoreContainer.RecordScore(new GameRecord
            {
                Date = DateTime.Now,

                SongChecksum = Song.Hash.HashBytes,
                SongName = Song.Name,
                SongArtist = Song.Artist,
                SongCharter = Song.Charter,

                ReplayFileName = replayInfo?.ReplayName,
                ReplayChecksum = replayInfo?.ReplayChecksum.HashBytes,

                BandScore = BandScore,
                BandStars = StarAmountHelper.GetStarsFromInt((int) BandStars),

                SongSpeed = SongSpeed
            }, playerEntries);
        }

        public void ForceQuitSong()
        {
            GlobalVariables.State = PersistentState.Default;
            GlobalVariables.Instance.LoadScene(SceneIndex.Menu);
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
            var data = new ReplayData(colorProfiles, cameraPresets, frames.ToArray());
            var (success, replayInfo) = ReplayIO.TrySerialize(directory, Song, SongSpeed, length, bandScore, stars, replayStats.ToArray(), data);
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
                    if ((!IsPractice || PracticeManager.HasSelectedSection) && !DialogManager.Instance.IsDialogShowing)
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
    }
}