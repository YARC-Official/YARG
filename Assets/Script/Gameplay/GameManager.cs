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
using YARG.Core.Game;
using YARG.Core.Input;
using YARG.Core.Logging;
using YARG.Core.Replays;
using YARG.Core.Song;
using YARG.Gameplay.HUD;
using YARG.Gameplay.Player;
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

        /// <inheritdoc cref="SongRunner.SelectedSongSpeed"/>
        public float SelectedSongSpeed => _songRunner.SelectedSongSpeed;

        /// <inheritdoc cref="SongRunner.Started"/>
        public bool Started => _songRunner.Started;

        /// <inheritdoc cref="SongRunner.Paused"/>
        public bool Paused => _songRunner.Paused;

        /// <inheritdoc cref="SongRunner.PendingPauses"/>
        public int PendingPauses => _songRunner.PendingPauses;

        public double SongLength { get; private set; }

        public bool IsReplay   { get; private set; }
        public bool IsPractice { get; private set; }

        public int   BandScore { get; private set; }
        public int   BandCombo { get; private set; }
        public float BandStars { get; private set; }

        public Replay Replay { get; private set; }

        public IReadOnlyList<BasePlayer> Players => _players;

        private bool _endingSong;

        private bool _isShowDebugText;
        private bool _isReplaySaved;

        private StemMixer _mixer;

        private void Awake()
        {
            // Set references
            PracticeManager = GetComponent<PracticeManager>();
            BackgroundManager = GetComponent<BackgroundManager>();

            YargPlayers = PlayerContainer.Players;

            Song = GlobalVariables.State.CurrentSong;
            IsReplay = GlobalVariables.State.IsReplay;
            IsPractice = GlobalVariables.State.IsPractice && !IsReplay;

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
        }

        private void OnDestroy()
        {
            YargLogger.LogInfo("Exiting song");

            if (Navigator.Instance != null)
            {
                Navigator.Instance.NavigationEvent -= OnNavigationEvent;
            }

            _mixer.SongEnd -= OnAudioEnd;
            _mixer.Dispose();
            AudioManager.UseMinimumStemVolume = false;

            _songRunner?.Dispose();
            BeatEventHandler?.Unsubscribe(StarPowerClap);
            BackgroundManager.Dispose();

            // Reset the time scale back, as it would be 0 at this point (because of pausing)
            Time.timeScale = 1f;
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
            BeatEventHandler.Update(_songRunner.RealSongTime);

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
            if (_songRunner.SongTime >= SongLength && !_endingSong)
            {
                EndSong().Forget();
            }

            // Debug text
            // Note: this must come last in the update sequence!
            // Any updates happening after this will not reflect until the next frame
            if (_isShowDebugText)
            {
                using var text = ZString.CreateStringBuilder(true);

                if (_players[0] is FiveFretPlayer fiveFretPlayer)
                {
                    var state = fiveFretPlayer.Engine.State;
                    var stats = fiveFretPlayer.Engine.EngineStats;

                    text.AppendFormat("Note index: {0}\n", state.NoteIndex);
                    text.AppendFormat("Buttons: {0}\n", state.ButtonMask);
                    text.AppendFormat("Star Power: {0:0.0000}\n", stats.StarPowerAmount);
                    text.AppendFormat("Ticks per beat: {0}\n", state.TicksEveryBeat);
                    text.AppendFormat("Ticks per measure: {0}\n", state.TicksEveryMeasure);
                }
                else if (_players[0] is DrumsPlayer drumsPlayer)
                {
                    var state = drumsPlayer.Engine.State;

                    text.AppendFormat("Note index: {0}\n", state.NoteIndex);
                }

                text.AppendFormat("Song time: {0:0.000000}\n", _songRunner.SongTime);
                text.AppendFormat("Audio time: {0:0.000000}\n", _songRunner.AudioTime);
                text.AppendFormat("Visual time: {0:0.000000}\n", _songRunner.VisualTime);
                text.AppendFormat("Input time: {0:0.000000}\n", _songRunner.InputTime);
                text.AppendFormat("Pause time: {0:0.000000}\n", _songRunner.PauseStartTime);
                text.AppendFormat("Sync difference: {0:0.000000}\n", _songRunner.SyncDelta);
                text.AppendFormat("Sync start delta: {0:0.000000}\n", _songRunner.SyncStartDelta);
                text.AppendFormat("Speed adjustment: {0:0.00}\n", _songRunner.SyncSpeedAdjustment);
                text.AppendFormat("Speed multiplier: {0}\n", _songRunner.SyncSpeedMultiplier);
                text.AppendFormat("Input base: {0:0.000000}\n", _songRunner.InputTimeBase);
                text.AppendFormat("Input offset: {0:0.000000}\n", _songRunner.InputTimeOffset);

                _debugText.SetText(text);
            }
        }

        public void SetSongTime(double time, double delayTime = SONG_START_DELAY)
        {
            _songRunner.SetSongTime(time, delayTime);

            BeatEventHandler.ResetTimers();
            BackgroundManager.SetTime(_songRunner.SongTime);
        }

        public void SetSongSpeed(float speed)
        {
            _songRunner.SetSongSpeed(speed);

            BackgroundManager.SetSpeed(_songRunner.SelectedSongSpeed);
        }

        public void AdjustSongSpeed(float deltaSpeed)
        {
            _songRunner.AdjustSongSpeed(deltaSpeed);

            BackgroundManager.SetSpeed(_songRunner.SelectedSongSpeed);
        }

        public void Pause(bool showMenu = true)
        {
            _songRunner.Pause();
            if (_songRunner.PendingPauses > 1) return;

            if (showMenu)
            {
                if (IsReplay)
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

            if (!IsReplay)
            {
                _debugText.gameObject.SetActive(false);
            }

            // Pause the background/venue
            Time.timeScale = 0f;
            BackgroundManager.SetPaused(true);
            GameStateFetcher.SetPaused(true);
        }

        public void Resume(bool inputCompensation = true)
        {
            _songRunner.Resume(inputCompensation);
            if (_songRunner.PendingPauses > 1) return;

            _pauseMenu.gameObject.SetActive(false);

            // Unpause the background/venue
            Time.timeScale = 1f;
            BackgroundManager.SetPaused(false);
            GameStateFetcher.SetPaused(false);

            _isReplaySaved = false;

            _debugText.gameObject.SetActive(_isShowDebugText);

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

        public void OverridePauseTime(double pauseTime = -1) => _songRunner.OverridePauseTime(pauseTime);

        public double GetRelativeInputTime(double timeFromInputSystem)
            => _songRunner.GetRelativeInputTime(timeFromInputSystem);

        public double GetCalibratedRelativeInputTime(double timeFromInputSystem)
            => _songRunner.GetCalibratedRelativeInputTime(timeFromInputSystem);

        private async UniTask EndSong()
        {
            if (_endingSong)
            {
                return;
            }

            if (IsPractice)
            {
                PracticeManager.ResetPractice();
                // Audio is paused automatically at this point, so we need to start it again
                _mixer.Play();
                return;
            }

            _endingSong = true;
            await UniTask.WaitUntil(() => InputTime >= SongLength + SONG_END_DELAY);

            if (IsReplay)
            {
                Pause(false);
                return;
            }

            // Pass the score info to the stats screen
            GlobalVariables.State.ScoreScreenStats = new ScoreScreenStats
            {
                PlayerScores = _players.Select(player => new PlayerScoreCard
                {
                    Player = player.Player,
                    Stats = player.BaseStats
                }).ToArray(),
                BandScore = BandScore,
                BandStars = (int) BandStars
            };

            (string Name, HashWrapper Hash)? replayInfo;
            try
            {
                _isReplaySaved = false;
                replayInfo = SaveReplay(Song.SongLengthSeconds, true);
            }
            catch (Exception e)
            {
                replayInfo = null;
                YargLogger.LogException(e, "Failed to save replay!");
            }

            // Get all of the individual player score entries
            var playerEntries = new List<PlayerScoreRecord>();
            foreach (var player in _players)
            {
                var profile = player.Player.Profile;

                // Skip bots
                if (player.Player.Profile.IsBot) continue;

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
                    IsFc = player.IsFc
                });
            }

            // Record the score into the database (if there's at least 1 non-bot player)
            if (playerEntries.Count > 0)
            {
                ScoreContainer.RecordScore(new GameRecord
                {
                    Date = DateTime.Now,

                    SongChecksum = Song.Hash.HashBytes,
                    SongName = Song.Name,
                    SongArtist = Song.Artist,
                    SongCharter = Song.Charter,

                    ReplayFileName = replayInfo?.Name,
                    ReplayChecksum = replayInfo?.Hash.HashBytes,

                    BandScore = BandScore,
                    BandStars = StarAmountHelper.GetStarsFromInt((int) BandStars),

                    SongSpeed = SelectedSongSpeed
                }, playerEntries);
            }

            // Go to the score screen
            GlobalVariables.Instance.LoadScene(SceneIndex.Score);
        }

        public void ForceQuitSong()
        {
            GlobalVariables.State = PersistentState.Default;
            GlobalVariables.Instance.LoadScene(SceneIndex.Menu);
        }

        public (string Name, HashWrapper Hash)? SaveReplay(double length, bool useScorePath)
        {
            var realPlayers = _players.Where(player => !player.Player.Profile.IsBot).ToList();

            if (_isReplaySaved || realPlayers.Count == 0)
            {
                return null;
            }

            var replay = ReplayContainer.CreateNewReplay(Song, realPlayers, length);
            var entry = ReplayContainer.CreateEntryFromReplayFile(new ReplayFile(replay));

            var name = entry.GetReplayName();

            if (useScorePath)
            {
                entry.ReplayPath = Path.Combine(ScoreContainer.ScoreReplayDirectory, name);
            }
            else
            {
                entry.ReplayPath = Path.Combine(ReplayContainer.ReplayDirectory, name);
            }

            var hash = ReplayIO.WriteReplay(entry.ReplayPath, replay);
            if (hash == null)
            {
                return null;
            }

            _isReplaySaved = true;
            return (name, hash.Value);
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

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && !Paused && SettingsManager.Settings.PauseOnFocusLoss.Value)
            {
                SetPaused(true);
            }
        }
    }
}