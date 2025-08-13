using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;
using YARG.Core;
using YARG.Core.Audio;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Core.Logging;
using YARG.Gameplay.HUD;
using YARG.Gameplay.Visuals;
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
        [FormerlySerializedAs("SoloPool")]
        [SerializeField]
        protected Pool EffectPool;

        public float ZeroFadePosition { get; private set; }
        public float FadeSize         { get; private set; }

        public Vector2 HUDViewportPosition => TrackCamera.WorldToViewportPoint(_hudLocation.position);

        protected List<Beatline> Beatlines;

        protected int BeatlineIndex;

        protected bool IsBass { get; private set; }

        private float _spawnAheadDelay;

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
    }

    public abstract class TrackPlayer<TEngine, TNote> : TrackPlayer
        where TEngine : BaseEngine
        where TNote : Note<TNote>
    {
        public TEngine Engine { get; private set; }

        public override BaseEngine BaseEngine => Engine;

        protected List<TNote> Notes { get; private set; }

        protected int NoteIndex { get; private set; }

        public InstrumentDifficulty<TNote> NoteTrack { get; private set; }

        private InstrumentDifficulty<TNote> OriginalNoteTrack { get; set; }

        private int _currentMultiplier;
        private int _previousMultiplier;

        private bool _isHotStartChecked;
        private bool _previousBassGrooveState;
        private bool _newHighScoreShown;

        private double _previousStarPowerAmount;

        private Queue<TrackEffect> _upcomingEffects = new();
        private List<TrackEffectElement> _currentEffects = new();
        private List<TrackEffect> _trackEffects = new();

        protected SongChart Chart;

        public override void Initialize(int index, YargPlayer player, SongChart chart, TrackView trackView,
            StemMixer mixer, int? currentHighScore)
        {
            if (IsInitialized)
            {
                return;
            }

            base.Initialize(index, player, chart, trackView, mixer, currentHighScore);

            SetupTheme(player.Profile.GameMode);

            Chart = chart;

            OriginalNoteTrack = GetNotes(chart);
            player.Profile.ApplyModifiers(OriginalNoteTrack);

            NoteTrack = OriginalNoteTrack;
            Notes = NoteTrack.Notes;

            var events = NoteTrack.TextEvents;

            Engine = CreateEngine();

            base.ComboMeter.Initialize(player.EnginePreset, Engine.BaseParameters.MaxMultiplier);

            Engine.OnComboIncrement += OnComboIncrement;
            Engine.OnComboReset += OnComboReset;
            if (GameManager.IsPractice)
            {
                Engine.SetSpeed(GameManager.SongSpeed >= 1 ? GameManager.SongSpeed : 1);
            }
            else if (Player.IsReplay)
            {
                // If it's a replay, the "SongSpeed" parameter should be set properly
                // when it gets deserialized. Transfer this over to the engine.
                Engine.SetSpeed(Player.EngineParameterOverride.SongSpeed);
            }
            else
            {
                Engine.SetSpeed(GameManager.SongSpeed);
            }

            InitializeTrackEffects();

            ResetNoteCounters();

            FinishInitialization();
        }

        private void InitializeTrackEffects()
        {
            var phrases = new List<Phrase>();

            foreach (var phrase in NoteTrack.Phrases)
            {
                // We only want solo and drum fill here. Unisons are added later
                // and there are no track effects for the other phrase types
                if (phrase.Type is PhraseType.Solo or PhraseType.DrumFill)
                {
                    // It turns out that some charts have drum fill phrases that aren't SP activation
                    // (they have no notes), so we need to ignore those
                    if (phrase.Type is PhraseType.DrumFill)
                    {
                        foreach (var note in Notes)
                        {
                            if (note.Time >= phrase.Time && note.Time <= phrase.TimeEnd)
                            {
                                phrases.Add(phrase);
                                break;
                            }
                        }
                    }
                    else
                    {
                        phrases.Add(phrase);
                    }
                }
            }

            phrases.AddRange(EngineContainer.UnisonPhrases);

            var effects = TrackEffect.PhrasesToEffects(phrases);
            _trackEffects.AddRange(effects);
            foreach (var effect in TrackEffect.SliceEffects(NoteSpeed, _trackEffects))
            {
                _upcomingEffects.Enqueue(effect);
            }

            if (EngineContainer.UnisonPhrases.Any())
            {
                GameManager.EngineManager.OnUnisonPhraseSuccess += OnUnisonPhraseSuccess;
            }
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
            TrackMaterial.Initialize(ZeroFadePosition, FadeSize, Player.HighwayPreset);
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

        protected override void UpdateVisuals(double visualTime)
        {
            UpdateNotes(visualTime);
            UpdateBeatlines(visualTime);
            UpdateTrackEffects(visualTime);

            var stats = Engine.BaseStats;

            int maxMultiplier = Engine.BaseParameters.MaxMultiplier;
            if (stats.IsStarPowerActive)
            {
                maxMultiplier *= 2;
            }

            double currentStarPowerAmount = Engine.GetStarPowerBarAmount();

            bool groove = stats.ScoreMultiplier == maxMultiplier;

            _currentMultiplier = stats.ScoreMultiplier;

            TrackMaterial.SetTrackScroll(visualTime, NoteSpeed);
            TrackMaterial.GrooveMode = groove;
            TrackMaterial.StarpowerMode = stats.IsStarPowerActive;

            ComboMeter.SetCombo(stats.ScoreMultiplier, maxMultiplier, stats.Combo);
            StarpowerBar.SetStarpower(currentStarPowerAmount, stats.IsStarPowerActive);
            SunburstEffects.SetSunburstEffects(groove, stats.IsStarPowerActive, _currentMultiplier);

            TrackView.UpdateNoteStreak(stats.Combo);


            // Could be if (!_isHotStartChecked && groove), but that would make it so hot start doesn't show
            // for bass until 6x.
            if (!_isHotStartChecked && stats.ScoreMultiplier == (!stats.IsStarPowerActive ? 4 : 8))
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

        private void UpdateNotes(double visualTime)
        {
            while (NoteIndex < Notes.Count && Notes[NoteIndex].Time <= visualTime + SpawnTimeOffset)
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

        private void UpdateBeatlines(double time)
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

        private void UpdateTrackEffects(double time)
        {
            if (_upcomingEffects.TryPeek(out var nextEffect) && nextEffect.Time <= time + SpawnTimeOffset)
            {
                SpawnEffect(nextEffect, false);
            }

            // If any of the current effects are drum fill, we need to react
            // when starpower goes from unavailable to available

            // Remove past effects from current list
            // This may actually fail if an effect is reused from the pool
            // too quickly, but as long as it is only being used for setting
            // drum fill visibility, it shouldn't break.
            for (var i = 0; i < _currentEffects.Count; i++)
            {
                if (!_currentEffects[i].Active)
                {
                    _currentEffects.RemoveAt(i);
                }
                else
                {
                    // See if it's an invisible drum fill and if starpower has become available
                    // Since we never change visibility on anything but drum fills, there's no need to check
                    // the effect type.
                    // TODO: We also need to change effects that were originally a UnisonAndDrumFill or SoloAndDrumFill
                    //  back from Unison or Solo, although I'm not even sure those exist. Maybe SoloAndDrumFill does..
                    if ((_currentEffects[i].Visibility < 1.0f && Engine.CanStarPowerActivate) && !Engine.BaseStats.IsStarPowerActive)
                    {
                        _currentEffects[i].MakeVisible();
                        // If start transition is disabled, previous should be disabled
                        if (!_currentEffects[i].EffectRef.StartTransitionEnable)
                        {
                            _currentEffects[i - 1].SetEndTransitionVisible(false);
                        }

                        // If end transition is disabled, next should be disabled if it is spawned
                        if (_currentEffects.Count > i + 1 && !_currentEffects[i].EffectRef.EndTransitionEnable)
                        {
                            _currentEffects[i + 1].SetStartTransitionVisible(false);
                        }
                    }
                    // We also need to make already spawned drum fills disappear if the player activated SP
                    // And we do need to check effect type here
                    if (_currentEffects[i].EffectRef.EffectType == TrackEffectType.DrumFill &&
                        (_currentEffects[i].Visibility == 1.0f && Engine.BaseStats.IsStarPowerActive))
                    {
                        _currentEffects[i].MakeVisible(false);

                        if (!_currentEffects[i].EffectRef.StartTransitionEnable && i > 0)
                        {
                            // Previous maybe needs end transition enabled since we're disappearing
                            // (if the effect type doesn't have an end transition set, it won't
                            //  be active regardless of what we do here, so a hard enable is ok)
                            _currentEffects[i - 1].SetEndTransitionVisible(true);
                        }

                        if (!_currentEffects[i].EffectRef.EndTransitionEnable)
                        {
                            // next needs start transition enabled, if it is spawned
                            // if it isn't yet spawned, it should already be set correctly
                            if (_currentEffects.Count > i + 1)
                            {
                                _currentEffects[i + 1].SetStartTransitionVisible(true);
                            }
                        }
                    }
                }
            }
        }

        private void SpawnEffect(TrackEffect nextEffect, bool seeking)
        {
            var poolable = EffectPool.TakeWithoutEnabling();
            if (poolable == null)
            {
                YargLogger.LogWarning("Attempted to spawn track effect, but it's at its cap!");
                return;
            }

            // The seeking code handles this for us if we're seeking
            if (!seeking)
            {
                _upcomingEffects.Dequeue();
            }

            // Do some magic to vanish drum fills if the player doesn't have enough SP to activate
            // or if SP is already active.

            if (Engine.BaseStats.IsStarPowerActive || !Engine.CanStarPowerActivate)
            {
                if (nextEffect.EffectType is TrackEffectType.DrumFill)
                {
                    nextEffect.Visibility = 0.0f;
                    if (!nextEffect.StartTransitionEnable)
                    {
                        if (_currentEffects.Count > 0)
                        {
                            _currentEffects[^1].SetEndTransitionVisible(true);
                            _currentEffects[^1].SetTransitionState();
                        }
                    }
                    if (!nextEffect.EndTransitionEnable)
                    {
                        // Get next next and turn on its start transition
                        // Since we are only spawning now, it shouldn't be possible
                        // for next next to be spawned yet.
                        if (_upcomingEffects.TryPeek(out var nextNextEffect))
                        {
                            nextNextEffect.StartTransitionEnable = true;
                        }
                    }

                    if (!nextEffect.StartTransitionEnable)
                    {
                        // Turn on end transition for previous effect

                        // Previous effect is by definition already spawned,
                        // but we'll check that _currentEffects isn't length zero
                        if (_currentEffects.Count > 0)
                        {
                            _currentEffects[^1].SetEndTransitionVisible(true);
                        }
                    }
                }

                if (nextEffect.EffectType is TrackEffectType.DrumFillAndUnison)
                {
                    nextEffect.EffectType = TrackEffectType.Unison;
                }

                if (nextEffect.EffectType is TrackEffectType.SoloAndDrumFill)
                {
                    nextEffect.EffectType = TrackEffectType.Solo;
                }
            }

            ((TrackEffectElement) poolable).EffectRef = nextEffect;
            _currentEffects.Add((TrackEffectElement) poolable);
            poolable.EnableFromPool();
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
            var shiftEvents = OriginalNoteTrack.RangeShiftEvents;

            NoteTrack = new InstrumentDifficulty<TNote>(instrument, difficulty, practiceNotes, phrases, textEvents, shiftEvents);
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

            // Reset the track effect overlay
            ResetTrackEffectOverlay(time);

            base.SetReplayTime(time);
        }

        private void ResetTrackEffectOverlay(double time)
        {
            // despawn any existing track effects, rebuild track effect structures, spawn any that are now in current
            _upcomingEffects.Clear();
            for(var i = 0; i < EffectPool.AllSpawned.Count; i++)
            {
                var poolable = EffectPool.AllSpawned[i];
                poolable.ParentPool.Return(poolable);
            }

            foreach (var effect in TrackEffect.SliceEffects(NoteSpeed, _trackEffects))
            {
                if (effect.Time >= time)
                {
                    _upcomingEffects.Enqueue(effect);
                } else if (effect.Time < time && time < effect.TimeEnd)
                {
                    // current effect, spawn it
                    SpawnEffect(effect, true);
                }
            }
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
            if (!GameManager.IsSeekingReplay)
            {
                SetStemMuteState(false);
                if (_currentMultiplier != _previousMultiplier)
                {
                    _previousMultiplier = _currentMultiplier;

                    foreach (var haptics in SantrollerHaptics)
                    {
                        haptics.SetMultiplier((byte) Math.Clamp(_currentMultiplier, 1, byte.MaxValue));
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
                haptic.SetSoloActive(true);
            }
        }

        protected virtual void OnSoloEnd(SoloSection solo)
        {
            TrackView.EndSolo(solo.SoloBonus);

            foreach (var haptic in SantrollerHaptics)
            {
                haptic.SetSoloActive(false);
            }
        }

        protected virtual void OnUnisonPhraseSuccess()
        {
            // This is here because it seemed like awarding from TrackPlayer would work best for replays
            // since all the replay data is saved here
            YargLogger.LogFormatTrace("TrackPlayer would have awarded unison bonus at engine time {0}", Engine.CurrentTime);
        }

        protected virtual void OnCountdownChange(double countdownLength, double endTime)
        {
            TrackView.UpdateCountdown(countdownLength, endTime);
        }

        protected virtual void OnStarPowerPhraseHit(TNote note)
        {
            OnStarPowerPhraseHit();
        }

        public override void GameplayUpdate()
        {
            base.GameplayUpdate();

            if (LastHighScore != null && !_newHighScoreShown && Score > LastHighScore)
            {
                _newHighScoreShown = true;
                TrackView.ShowNewHighScore();
            }
        }
    }
}
