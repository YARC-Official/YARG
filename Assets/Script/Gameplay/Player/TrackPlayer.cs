using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using YARG.Audio;
using YARG.Core;
using YARG.Core.Audio;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Core.Logging;
using YARG.Gameplay.HUD;
using YARG.Gameplay.Visuals;
using YARG.Player;
using YARG.Themes;

namespace YARG.Gameplay.Player
{
    public abstract class TrackPlayer : BasePlayer
    {
        public const float STRIKE_LINE_POS       = -2f;
        public const float DEFAULT_ZERO_FADE_POS = 3f;
        public const float NOTE_SPAWN_OFFSET     = 5f;

        public const float TRACK_WIDTH = 2f;

        public double SpawnTimeOffset => (ZeroFadePosition + 2 + -STRIKE_LINE_POS) / NoteSpeed;

        protected TrackView TrackView { get; private set; }

        protected int? CurrentHighScore { get; private set; }

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

        public float ZeroFadePosition { get; private set; }
        public float FadeSize         { get; private set; }

        public Vector2 HUDViewportPosition => TrackCamera.WorldToViewportPoint(_hudLocation.position);

        protected List<Beatline> Beatlines;
        protected int BeatlineIndex;

        protected bool IsHotStartChecked;
        protected bool PreviousBassGrooveState;
        protected double PreviousStarPowerAmount;

        protected bool IsBass;

        public virtual void Initialize(int index, YargPlayer player, SongChart chart, TrackView trackView, StemMixer mixer, int? currentHighScore)
        {
            if (IsInitialized) return;

            Initialize(index, player, chart);

            TrackView = trackView;

            CurrentHighScore = currentHighScore;

            Beatlines = SyncTrack.Beatlines;
            BeatlineIndex = 0;

            var preset = player.EnginePreset;
            IndicatorStripes.Initialize(preset);
            ComboMeter.Initialize(preset);

            // Set fade information and highway length
            ZeroFadePosition = DEFAULT_ZERO_FADE_POS * Player.Profile.HighwayLength;
            FadeSize = Player.CameraPreset.FadeLength;
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

        protected override void UpdateVisualsWithTimes(double time)
        {
            base.UpdateVisualsWithTimes(time);
            UpdateNotes(time);
            UpdateBeatlines(time);
        }

        protected abstract void UpdateNotes(double time);

        private void UpdateBeatlines(double time)
        {
            while (BeatlineIndex < Beatlines.Count && Beatlines[BeatlineIndex].Time <= time + SpawnTimeOffset)
            {
                var beatline = Beatlines[BeatlineIndex];

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

        public override void Initialize(int index, YargPlayer player, SongChart chart, TrackView trackView, StemMixer mixer, int? currentHighScore)
        {
            if (IsInitialized) return;

            base.Initialize(index, player, chart, trackView, mixer, currentHighScore);

            SetupTheme(player.Profile.GameMode);

            OriginalNoteTrack = GetNotes(chart);
            player.Profile.ApplyModifiers(OriginalNoteTrack);

            NoteTrack = OriginalNoteTrack;
            Notes = NoteTrack.Notes;

            Engine = CreateEngine();

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

            TrackMaterial.Initialize(ZeroFadePosition, FadeSize);
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

            bool groove = stats.ScoreMultiplier == maxMultiplier;

            _currentMultiplier = stats.ScoreMultiplier;

            TrackMaterial.SetTrackScroll(songTime, NoteSpeed);
            TrackMaterial.GrooveMode = groove;
            TrackMaterial.StarpowerMode = stats.IsStarPowerActive;

            ComboMeter.SetCombo(stats.ScoreMultiplier, maxMultiplier, stats.Combo);
            StarpowerBar.SetStarpower(stats.StarPowerAmount, stats.IsStarPowerActive);
            SunburstEffects.SetSunburstEffects(groove, stats.IsStarPowerActive);

            TrackView.UpdateNoteStreak(stats.Combo);

            if (!IsHotStartChecked && stats.ScoreMultiplier == 4)
            {
                IsHotStartChecked = true;

                if (IsFc)
                {
                    TrackView.ShowHotStart();
                }
            }

            bool currentBassGrooveState = IsBass && groove;

            if (!PreviousBassGrooveState && currentBassGrooveState)
            {
                TrackView.ShowBassGroove();
            }

            PreviousBassGrooveState = currentBassGrooveState;

            double currentStarPowerAmount = stats.StarPowerAmount;

            if (!stats.IsStarPowerActive && PreviousStarPowerAmount < 0.5 && currentStarPowerAmount >= 0.5)
            {
                TrackView.ShowStarPowerReady();
            }

            PreviousStarPowerAmount = currentStarPowerAmount;

            foreach (var haptics in SantrollerHaptics)
            {
                haptics.SetStarPowerFill((float) BaseStats.StarPowerAmount);
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

                // Don't spawn hit or missed notes
                if (note.WasHit || note.WasMissed)
                {
                    continue;
                }

                // Spawn all of the notes and child notes
                foreach (var child in note.ChordEnumerator())
                {
                    SpawnNote(child);
                }
            }
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

            Beatlines = SyncTrack.Beatlines.Where(b => b.Tick >= start && b.Tick <= end).ToList();
            BeatlineIndex = 0;

            Engine = CreateEngine();
            ResetPracticeSection();
        }

        public override void SetReplayTime(double time)
        {
            BeatlineIndex = 0;
            ResetNoteCounters();

            base.SetReplayTime(time);
        }

        protected void SpawnNote(TNote note)
        {
            var poolable = NotePool.KeyedTakeWithoutEnabling(note);
            if (poolable == null)
            {
                YargLogger.LogWarning("Attempted to spawn note, but it's at its cap!");
                return;
            }

            InitializeSpawnedNote(poolable, note);
            poolable.EnableFromPool();
        }

        protected abstract void InitializeSpawnedNote(IPoolable poolable, TNote note);

        protected virtual void OnNoteHit(int index, TNote note)
        {
            SetStemMuteState(false);
            if (_currentMultiplier != _previousMultiplier)
            {
                _previousMultiplier = _currentMultiplier;

                foreach (var haptics in SantrollerHaptics)
                {
                    haptics.SetMultiplier((uint)_currentMultiplier);
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

            _lastCombo = Combo;
        }

        protected virtual void OnNoteMissed(int index, TNote note)
        {
            if (IsFc)
            {
                ComboMeter.SetFullCombo(false);
                IsFc = false;
            }

            SetStemMuteState(true);

            if (_lastCombo >= 10)
            {
                AudioManager.PlaySoundEffect(SfxSample.NoteMiss);
            }

            _lastCombo = Combo;

            foreach (var haptics in SantrollerHaptics)
            {
                haptics.SetMultiplier(0);
            }
        }

        protected virtual void OnOverstrum()
        {
            if (IsFc)
            {
                ComboMeter.SetFullCombo(false);
                IsFc = false;
            }

            _lastCombo = Combo;
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

            foreach (var haptic in SantrollerHaptics)
            {
                haptic.SetSolo(false);
            }
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

            if (CurrentHighScore != null && !IsNewHighScore && Score > CurrentHighScore)
            {
                IsNewHighScore = true;
                TrackView.ShowNewHighScore();
            }
        }
    }
}