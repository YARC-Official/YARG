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
using YARG.Core.Game;
using YARG.Core.Input;
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

        private IReadOnlyList<YargPlayer> _yargPlayers;
        private List<BasePlayer>          _players;

        public bool IsSongStarted { get; private set; } = false;

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

        /// <inheritdoc cref="SongRunner.Paused"/>
        public bool Paused => _songRunner.Paused;

        /// <inheritdoc cref="SongRunner.PendingPauses"/>
        public int PendingPauses => _songRunner.PendingPauses;

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
                return;
            }

            // Hide vocals track (will be shown when players are initialized
            VocalTrack.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Navigator.Instance != null)
            {
                Navigator.Instance.NavigationEvent -= OnNavigationEvent;
            }
            GlobalVariables.AudioManager.SongEnd -= OnAudioEnd;
            _songRunner.Dispose();
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
            BandStars = _players.Sum(player => player.GetStarsPercent()) / _players.Count;

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
                    var ticksPerBeat = fiveFretPlayer.Engine.State.TicksEveryBeat;
                    var ticksPerMeasure = fiveFretPlayer.Engine.State.TicksEveryMeasure;
                    double starPower = fiveFretPlayer.Engine.EngineStats.StarPowerAmount;

                    _debugText.text +=
                        $"Note index: {noteIndex}\n" +
                        $"Buttons: {buttonMask}\n" +
                        $"Star Power: {starPower:0.0000}\n" +
                        $"Ticks per beat: {ticksPerBeat}\n" +
                        $"Ticks per measure: {ticksPerMeasure}\n";
                }
                else if (_players[0] is DrumsPlayer drumsPlayer)
                {
                    int noteIndex = drumsPlayer.Engine.State.NoteIndex;

                    _debugText.text +=
                        $"Note index: {noteIndex}\n";
                }

                _debugText.text +=
                    $"Song time: {_songRunner.SongTime:0.000000}\n" +
                    $"Visual time: {_songRunner.VisualTime:0.000000}\n" +
                    $"Input time: {_songRunner.InputTime:0.000000}\n" +
                    $"Pause time: {_songRunner.PauseStartTime:0.000000}\n" +
                    $"Sync difference: {_songRunner.SyncVisualTime - _songRunner.SyncSongTime:0.000000}\n" +
                    $"Sync start delta: {_songRunner.SyncStartDelta:0.000000}\n" +
                    $"Speed adjustment: {_songRunner.SyncSpeedAdjustment:0.00}\n" +
                    $"Speed multiplier: {_songRunner.SyncSpeedMultiplier}\n" +
                    $"Input base: {_songRunner.InputTimeBase:0.000000}\n" +
                    $"Input offset: {_songRunner.InputTimeOffset:0.000000}\n";
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

        private void EndSong()
        {
            if (IsPractice)
            {
                PracticeManager.ResetPractice();
                return;
            }

            GlobalVariables.AudioManager.UnloadSong();

            if (IsReplay) return;

            // Pass the score info to the stats screen
            GlobalVariables.Instance.ScoreScreenStats = new ScoreScreenStats
            {
                PlayerScores = _players.Select(player => new PlayerScoreCard
                {
                    Player = player.Player,
                    Stats = player.BaseStats
                }).ToArray(),
                BandScore = BandScore,
                BandStars = (int) BandStars
            };

            _isReplaySaved = false;
            var replayInfo = SaveReplay(Song.SongLengthSeconds, true);

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
                    Stars = StarAmountHelper.GetStarsFromInt(player.BaseStats.Stars),

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
            GlobalVariables.Instance.IsReplay = false;
            GlobalVariables.Instance.LoadScene(SceneIndex.Score);
        }

        public void ForceQuitSong()
        {
            GlobalVariables.AudioManager.UnloadSong();

            GlobalVariables.Instance.IsReplay = false;
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

            Debug.Log("Wrote replay");
            _isReplaySaved = true;

            // If the hash could not be retrieved, return null
            if (hash == null)
            {
                return null;
            }

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
    }
}