using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using YARG.Audio;
using YARG.Core;
using YARG.Core.Audio;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Core.Game;
using YARG.Core.Logging;
using YARG.Gameplay.HUD;
using YARG.Gameplay.Visuals;
using YARG.Helpers.Extensions;
using YARG.Player;
using YARG.Settings;
using YARG.Themes;

namespace YARG.Gameplay.Player
{
    public abstract class TrackPlayer : BasePlayer
    {
        public const float STRIKE_LINE_POS       = -2f;
        public const float DEFAULT_ZERO_FADE_POS = 3f;
        public const float NOTE_SPAWN_OFFSET     = 5f;

        public const float TRACK_WIDTH = 2f;

        public double SpawnTimeOffset => (ZeroFadePosition + _spawnAheadDelay + -STRIKE_LINE_POS) / NoteSpeed;

        protected TrackView TrackView { get; private set; }

        [field: Header("Visuals")]
        [field: SerializeField]
        public Camera TrackCamera { get; private set; }

        [SerializeField]
        protected CameraPositioner CameraPositioner;
        [SerializeField]
        protected TrackMaterial TrackMaterial;
        [SerializeField]
        protected ComboMeter ComboMeter;
        [SerializeField]
        protected StarpowerBar StarpowerBar;
        [SerializeField]
        protected SunburstEffects SunburstEffects;
        [SerializeField]
        protected IndicatorStripes IndicatorStripes;
        [SerializeField]
        protected HitWindowDisplay HitWindowDisplay;

        [SerializeField]
        private Transform _hudLocation;

        [Header("Pools")]
        [SerializeField]
        protected KeyedPool NotePool;
        [SerializeField]
        protected Pool BeatlinePool;
        [SerializeField]
        protected Pool SoloPool;

        public float ZeroFadePosition { get; private set; }
        public float FadeSize         { get; private set; }

        public Vector2 HUDViewportPosition => TrackCamera.WorldToViewportPoint(_hudLocation.position);

        protected List<Beatline> Beatlines;

        protected int BeatlineIndex;

        protected bool IsBass { get; private set; }

        private float _spawnAheadDelay;

        public struct Solo
        {
            public Solo(double startTime, double endTime)
            {
                StartTime = startTime;
                EndTime = endTime;
                Started = false;
                Finished = false;
            }

            public readonly double StartTime;
            public readonly double EndTime;
            public bool Started;
            public bool Finished;
        }

        public virtual void Initialize(int index, YargPlayer player, SongChart chart, TrackView trackView,
            StemMixer mixer, int? lastHighScore)
        {
            if (IsInitialized)
            {
                return;
            }

            Initialize(index, player, chart, lastHighScore);

            TrackView = trackView;

            Beatlines = SyncTrack.Beatlines;
            BeatlineIndex = 0;

            var preset = player.EnginePreset;
            IndicatorStripes.Initialize(preset);
            ComboMeter.Initialize(preset);

            // Set fade information and highway length
            ZeroFadePosition = DEFAULT_ZERO_FADE_POS * Player.Profile.HighwayLength;
            FadeSize = Player.CameraPreset.FadeLength;

            _spawnAheadDelay = GameManager.IsPractice ? SettingsManager.Settings.PracticeRestartDelay.Value : 2;
            if (player.Profile.HighwayLength > 1)
            {
                FadeSize *= player.Profile.HighwayLength;
            }

            // Move the HUD location based on the highway length
            var change = ZeroFadePosition - DEFAULT_ZERO_FADE_POS;
            _hudLocation.position = _hudLocation.position.AddZ(change);

            // Determine if a track is bass or not for the BASS GROOVE text notification
            IsBass = Player.Profile.CurrentInstrument
                is Instrument.FiveFretBass
                or Instrument.SixFretBass
                or Instrument.ProBass_17Fret
                or Instrument.ProBass_22Fret;

            TrackView.ShowPlayerName(player);
        }

        protected override void UpdateVisualsWithTimes(double time)
        {
            base.UpdateVisualsWithTimes(time);
            UpdateNotes(time);
            UpdateBeatlines(time);
            UpdateSolos(time);
        }

        protected override void ResetVisuals()
        {
            // "Muting a stem" isn't technically a visual,
            // but it's a form of feedback so we'll put it here.
            SetStemMuteState(false);

            ComboMeter.SetFullCombo(IsFc);
            TrackView.ForceReset();

            NotePool.ReturnAllObjects();
            BeatlinePool.ReturnAllObjects();

            HitWindowDisplay.SetHitWindowSize();
        }

        protected abstract void UpdateNotes(double time);

        protected abstract void UpdateBeatlines(double time);

        protected abstract void UpdateSolos(double time);
    }

    public abstract class TrackPlayer<TEngine, TNote> : TrackPlayer
        where TEngine : BaseEngine
        where TNote : Note<TNote>
    {
        public TEngine Engine { get; private set; }

        public override BaseEngine BaseEngine => Engine;

        protected List<TNote> Notes { get; private set; }

        protected int NoteIndex { get; private set; }

        protected InstrumentDifficulty<TNote> NoteTrack { get; private set; }

        private InstrumentDifficulty<TNote> OriginalNoteTrack { get; set; }

        private int _currentMultiplier;
        private int _previousMultiplier;

        private bool _isHotStartChecked;
        private bool _previousBassGrooveState;
        private bool _newHighScoreShown;

        private double _previousStarPowerAmount;

        private Queue<Solo> _upcomingSolos = new();
        private Stack<Solo> _previousSolos;
        private Queue<Solo> _currentSolos;

        private bool _isSoloActive = false;
        private bool _isSoloStarting = false;
        private bool _isSoloEnding = false;
        private double _nextSoloStartTime = 0;
        private double _nextSoloEndTime = 0;

        public override void Initialize(int index, YargPlayer player, SongChart chart, TrackView trackView,
            StemMixer mixer, int? currentHighScore)
        {
            if (IsInitialized)
            {
                return;
            }

            base.Initialize(index, player, chart, trackView, mixer, currentHighScore);

            SetupTheme(player.Profile.GameMode);

            OriginalNoteTrack = GetNotes(chart);
            player.Profile.ApplyModifiers(OriginalNoteTrack);

            NoteTrack = OriginalNoteTrack;
            Notes = NoteTrack.Notes;

            Engine = CreateEngine();

            if (GameManager.IsPractice)
            {
                Engine.SetSpeed(GameManager.SongSpeed >= 1 ? GameManager.SongSpeed : 1);
            }
            else if (GameManager.ReplayInfo != null)
            {
                // If it's a replay, the "SongSpeed" parameter should be set properly
                // when it gets deserialized. Transfer this over to the engine.
                Engine.SetSpeed(Player.EngineParameterOverride.SongSpeed);
            }
            else
            {
                Engine.SetSpeed(GameManager.SongSpeed);
            }

            foreach(var soloSection in Engine.GetSolos())
            {
                _upcomingSolos.Enqueue(new Solo(soloSection.StartTime, soloSection.EndTime));
            }
            if (_upcomingSolos.Any())
            {
                _nextSoloStartTime = _upcomingSolos.Peek().StartTime;
                _nextSoloEndTime = _upcomingSolos.Peek().EndTime;
            }

            var soloCount = _upcomingSolos.Count();

            // Preallocate solo queue and stack
            _currentSolos = new Queue<Solo>(soloCount);
            _previousSolos = new Stack<Solo>(soloCount);

            ResetNoteCounters();

            FinishInitialization();
        }

        private void SetupTheme(GameMode gameMode)
        {
            var themePrefab = ThemeManager.Instance.CreateNotePrefabFromTheme(
                Player.ThemePreset, gameMode, NotePool.Prefab);
            NotePool.SetPrefabAndReset(themePrefab);
        }

        protected abstract InstrumentDifficulty<TNote> GetNotes(SongChart chart);
        protected abstract TEngine CreateEngine();

        protected virtual void FinishInitialization()
        {
            GameManager.BeatEventHandler.Subscribe(StarpowerBar.PulseBar);

            TrackMaterial.Initialize(ZeroFadePosition, FadeSize, Player.HighwayPreset, GameManager);
            CameraPositioner.Initialize(Player.CameraPreset);
        }

        protected void ResetNoteCounters()
        {
            NoteIndex = 0;
            TotalNotes = Notes.Sum(i => Engine.GetNumberOfNotes(i));
        }

        public override void ResetPracticeSection()
        {
            Engine.Reset(true);

            if (NoteTrack.Notes.Count > 0)
            {
                NoteTrack.Notes[0].OverridePreviousNote();
                NoteTrack.Notes[^1].OverrideNextNote();
            }

            BeatlineIndex = 0;
            ResetNoteCounters();

            base.ResetPracticeSection();
        }

        protected void UpdateBaseVisuals(BaseStats stats, BaseEngineParameters engineParams, double songTime)
        {
            int maxMultiplier = engineParams.MaxMultiplier;
            if (stats.IsStarPowerActive)
            {
                maxMultiplier *= 2;
            }

            double currentStarPowerAmount = Engine.GetStarPowerBarAmount();

            bool groove = stats.ScoreMultiplier == maxMultiplier;

            _currentMultiplier = stats.ScoreMultiplier;

            TrackMaterial.SetTrackScroll(songTime, NoteSpeed);
            TrackMaterial.GrooveMode = groove;
            TrackMaterial.StarpowerMode = stats.IsStarPowerActive;

            ComboMeter.SetCombo(stats.ScoreMultiplier, maxMultiplier, stats.Combo);
            StarpowerBar.SetStarpower(currentStarPowerAmount, stats.IsStarPowerActive);
            SunburstEffects.SetSunburstEffects(groove, stats.IsStarPowerActive);

            TrackView.UpdateNoteStreak(stats.Combo);

            if (!_isHotStartChecked && stats.ScoreMultiplier == 4)
            {
                _isHotStartChecked = true;

                if (IsFc)
                {
                    TrackView.ShowHotStart();
                }
            }

            bool currentBassGrooveState = IsBass && groove;

            if (!_previousBassGrooveState && currentBassGrooveState)
            {
                TrackView.ShowBassGroove();
            }

            _previousBassGrooveState = currentBassGrooveState;

            if (!stats.IsStarPowerActive && _previousStarPowerAmount < 0.5 && currentStarPowerAmount >= 0.5)
            {
                TrackView.ShowStarPowerReady();
            }

            _previousStarPowerAmount = currentStarPowerAmount;

            foreach (var haptics in SantrollerHaptics)
            {
                haptics.SetStarPowerFill((float) currentStarPowerAmount);
            }
        }

        protected override void UpdateNotes(double songTime)
        {
            while (NoteIndex < Notes.Count && Notes[NoteIndex].Time <= songTime + SpawnTimeOffset)
            {
                var note = Notes[NoteIndex];

                // Skip this frame if the pool is full
                if (!NotePool.CanSpawnAmount(note.ChildNotes.Count + 1))
                {
                    break;
                }

                NoteIndex++;

                OnNoteSpawned(note);

                // Don't spawn hit or missed notes
                if (note.WasHit || note.WasMissed)
                {
                    continue;
                }

                // Spawn all of the notes and child notes
                foreach (var child in note.AllNotes)
                {
                    SpawnNote(child);
                }
            }
        }

        protected override void UpdateBeatlines(double time)
        {
            while (BeatlineIndex < Beatlines.Count && Beatlines[BeatlineIndex].Time <= time + SpawnTimeOffset)
            {
                if (BeatlineIndex + 1 < Beatlines.Count && Beatlines[BeatlineIndex + 1].Time <= time + SpawnTimeOffset)
                {
                    BeatlineIndex++;
                    continue;
                }

                var beatline = Beatlines[BeatlineIndex];

                if (Notes.Count > 0 && beatline.Time > Notes[^1].TimeEnd)
                {
                    return;
                }

                // Skip this frame if the pool is full
                if (!BeatlinePool.CanSpawnAmount(1))
                {
                    break;
                }

                var poolable = BeatlinePool.TakeWithoutEnabling();
                if (poolable == null)
                {
                    YargLogger.LogWarning("Attempted to spawn beatline, but it's at its cap!");
                    break;
                }

                ((BeatlineElement) poolable).BeatlineRef = beatline;
                poolable.EnableFromPool();

                BeatlineIndex++;
            }
        }

        protected override void UpdateSolos(double time)
        {
            if (!_upcomingSolos.TryPeek(out var nextSolo))
            {
                return;
            }

            if (!(nextSolo.StartTime <= time + SpawnTimeOffset))
            {
                return;
            }

            SpawnSolo(nextSolo, false);
        }

        private void SpawnSolo(Solo nextSolo, bool seeking)
        {
            var poolable = SoloPool.TakeWithoutEnabling();
            if (poolable == null)
            {
                YargLogger.LogWarning("Attempted to spawn solo, but it's at its cap!");
                return;
            }

            ((SoloElement) poolable).SoloRef = nextSolo;
            poolable.EnableFromPool();
            // The seeking code handles this for us if we're seeking
            if (!seeking)
            {
                _currentSolos.Enqueue(nextSolo);
                _upcomingSolos.Dequeue();
            }
        }

        public float ZFromTime(double time)
        {
            float z = STRIKE_LINE_POS + (float) (time - GameManager.RealVisualTime) * NoteSpeed;
            return z;
        }

        protected virtual void OnNoteSpawned(TNote parentNote)
        {
        }

        public override void SetPracticeSection(uint start, uint end)
        {
            var practiceNotes = OriginalNoteTrack.Notes.Where(n => n.Tick >= start && n.Tick < end).ToList();

            YargLogger.LogFormatDebug("Practice notes: {0}", practiceNotes.Count);

            var instrument = OriginalNoteTrack.Instrument;
            var difficulty = OriginalNoteTrack.Difficulty;
            var phrases = OriginalNoteTrack.Phrases;
            var textEvents = OriginalNoteTrack.TextEvents;

            NoteTrack = new InstrumentDifficulty<TNote>(instrument, difficulty, practiceNotes, phrases, textEvents);
            Notes = NoteTrack.Notes;

            ResetNoteCounters();

            BeatlineIndex = 0;

            Engine = CreateEngine();

            if (GameManager.IsPractice)
            {
                Engine.SetSpeed(GameManager.SongSpeed >= 1 ? GameManager.SongSpeed : 1);
            }
            else
            {
                Engine.SetSpeed(GameManager.SongSpeed);
            }

            ResetPracticeSection();
        }

        public override void SetReplayTime(double time)
        {
            BeatlineIndex = 0;
            ResetNoteCounters();

            // Reset the solo overlay
            ResetSoloOverlay(time);

            base.SetReplayTime(time);
        }

        private void ResetSoloOverlay(double time)
        {
            // despawn any existing solos, rebuild solo structures, spawn any that are now in current
            _upcomingSolos.Clear();
            _currentSolos.Clear();
            // We can just get rid of previous solos then, we won't be using it (unless there are problems with the naive approach)
            _previousSolos.Clear();
            foreach (var soloSection in Engine.GetSolos())
            {
                if (soloSection.StartTime > time)
                {
                    // It hasn't happened yet, so queue for later
                    _upcomingSolos.Enqueue(new Solo(soloSection.StartTime, soloSection.EndTime));
                }
                else if (soloSection.EndTime < time)
                {
                    // It has already happened, so push it on the completed stack
                    _previousSolos.Push(new Solo(soloSection.StartTime, soloSection.EndTime));
                }
                else
                {
                    // It must be current, so we need to spawn it here
                    var solo = new Solo(soloSection.StartTime, soloSection.EndTime);
                    _currentSolos.Enqueue(solo);
                    SpawnSolo(solo, true);
                }
            }
        }

        protected BaseElement SpawnNote(TNote note)
        {
            var poolable = NotePool.KeyedTakeWithoutEnabling(note);
            if (poolable == null)
            {
                YargLogger.LogWarning("Attempted to spawn note, but it's at its cap!");
                return (BaseElement) null;
            }

            InitializeSpawnedNote(poolable, note);
            poolable.EnableFromPool();
            return (BaseElement) poolable;
        }

        protected abstract void InitializeSpawnedNote(IPoolable poolable, TNote note);

        protected virtual void OnNoteHit(int index, TNote note)
        {
            if (!GameManager.IsSeekingReplay)
            {
                SetStemMuteState(false);
                if (_currentMultiplier != _previousMultiplier)
                {
                    _previousMultiplier = _currentMultiplier;

                    foreach (var haptics in SantrollerHaptics)
                    {
                        haptics.SetMultiplier((uint) _currentMultiplier);
                    }
                }

                if (index >= Notes.Count - 1 && note.ParentOrSelf.WasFullyHit())
                {
                    if (IsFc)
                    {
                        TrackView.ShowFullCombo();
                    }
                    else if (Combo >= 30) // 30 to coincide with 4x multiplier (including on bass)
                    {
                        TrackView.ShowStrongFinish();
                    }
                }
            }

            LastCombo = Combo;
        }

        protected virtual void OnNoteMissed(int index, TNote note)
        {
            if (IsFc)
            {
                ComboMeter.SetFullCombo(false);
                IsFc = false;
            }

            if (!GameManager.IsSeekingReplay)
            {
                SetStemMuteState(true);

                if (LastCombo >= 10)
                {
                    GlobalAudioHandler.PlaySoundEffect(SfxSample.NoteMiss);
                }

                foreach (var haptics in SantrollerHaptics)
                {
                    haptics.SetMultiplier(0);
                }
            }

            LastCombo = Combo;
        }

        protected virtual void OnOverhit()
        {
            if (IsFc)
            {
                ComboMeter.SetFullCombo(false);
                IsFc = false;
            }

            LastCombo = Combo;
        }

        protected virtual void OnSoloStart(SoloSection solo)
        {
            TrackView.StartSolo(solo);

            foreach (var haptic in SantrollerHaptics)
            {
                haptic.SetSolo(true);
            }
        }

        protected virtual void OnSoloEnd(SoloSection solo)
        {
            TrackView.EndSolo(solo.SoloBonus);
            // TODO: This isn't being used yet, but needs to exist when
            //  we're actually handling replays properly. I'm not sure
            //  anything other than the upcoming queue actually needs to
            //  exist
            if (_currentSolos.TryPeek(out var lastSolo))
            {
                _previousSolos.Push(_currentSolos.Dequeue());
            }

            foreach (var haptic in SantrollerHaptics)
            {
                haptic.SetSolo(false);
            }
        }

        protected virtual void OnCountdownChange(int measuresLeft, double countdownLength, double endTime)
        {
            TrackView.UpdateCountdown(measuresLeft, countdownLength, endTime);
        }

        protected virtual void OnStarPowerPhraseHit(TNote note)
        {
            OnStarPowerPhraseHit();
        }

        protected override void FinishDestruction()
        {
            base.FinishDestruction();

            GameManager.BeatEventHandler.Unsubscribe(StarpowerBar.PulseBar);
        }

        public override void UpdateWithTimes(double inputTime)
        {
            base.UpdateWithTimes(inputTime);

            if (LastHighScore != null && !_newHighScoreShown && Score > LastHighScore)
            {
                _newHighScoreShown = true;
                TrackView.ShowNewHighScore();
            }
        }
    }
}