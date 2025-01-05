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

        public enum TrackEffectType
        {
            Solo,
            Unison,
            SoloAndUnison,
        }
        public struct TrackEffect
        {
            public TrackEffect(double startTime, double endTime, TrackEffectType effectType,
                bool startTransitionEnable = true, bool endTransitionEnable = true)
            {
                StartTime = startTime;
                EndTime = endTime;
                EffectType = effectType;
                StartTransitionEnable = startTransitionEnable;
                EndTransitionEnable = endTransitionEnable;
            }
            public readonly double StartTime;
            public readonly double EndTime;
            public readonly TrackEffectType EffectType;
            public readonly bool StartTransitionEnable;
            public readonly bool EndTransitionEnable;
        }

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
            UpdateTrackEffects(time);
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

        protected abstract void UpdateTrackEffects(double time);
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

        private Queue<Solo> _upcomingSolos = new();
        private List<TrackEffect> _trackEffectList = new();
        private Queue<TrackEffect> _upcomingEffects = new();

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

            // We have to get the solos here in case there aren't other
            // players and we never receive the OnUnisonPhrasesReady event
            foreach(var soloSection in Engine.GetSolos())
            {
                _upcomingSolos.Enqueue(new Solo(soloSection.StartTime, soloSection.EndTime));
                _upcomingEffects.Enqueue(new TrackEffect(soloSection.StartTime, soloSection.EndTime,
                    TrackEffectType.Solo));
            }

            // We have to subscribe to the event before calling
            // AddStarPowerSections or we will miss the event if we
            // happen to be the last player to initialize
            GameManager.OnUnisonPhrasesReady += OnUnisonPhrasesReady;
            GameManager.AddStarPowerSections(NoteTrack.Phrases, this);

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

        protected override void UpdateTrackEffects(double time)
        {
            if (!_upcomingEffects.TryPeek(out var nextEffect))
            {
                return;
            }

            if (!(nextEffect.StartTime <= time + SpawnTimeOffset))
            {
                return;
            }

            SpawnEffect(nextEffect, false);
        }

        private void SpawnEffect(TrackEffect nextEffect, bool seeking)
        {
            var poolable = SoloPool.TakeWithoutEnabling();
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

            ((TrackEffectElement) poolable).EffectRef = nextEffect;
            poolable.EnableFromPool();
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
            ResetTrackEffectOverlay(time);

            base.SetReplayTime(time);
        }

        private void ResetTrackEffectOverlay(double time)
        {
            // TODO: Make this handle unisons, probably by keeping a list of effects
            //  that doesn't change instead of using a queue
            // despawn any existing solos, rebuild solo structures, spawn any that are now in current
            _upcomingEffects.Clear();
            for(var i = 0; i < SoloPool.AllSpawned.Count; i++)
            {
                var poolable = SoloPool.AllSpawned[i];
                poolable.ParentPool.Return(poolable);
            }

            foreach (var soloSection in Engine.GetSolos())
            {
                if (soloSection.StartTime > time)
                {
                    // It hasn't happened yet, so queue for later
                    _upcomingEffects.Enqueue(new TrackEffect(soloSection.StartTime, soloSection.EndTime, TrackEffectType.Solo));
                }
                else if (soloSection.EndTime < time)
                {
                    // We don't need to do anything here
                }
                else
                {
                    // It must be current, so we need to spawn it here
                    var effect = new TrackEffect(soloSection.StartTime, soloSection.EndTime, TrackEffectType.Solo);
                    SpawnEffect(effect, true);
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

            foreach (var haptic in SantrollerHaptics)
            {
                haptic.SetSolo(false);
            }
        }

        protected virtual void OnUnisonPhrasesReady(List<GameManager.UnisonEvent> unisonEvents)
        {
            MergeTrackEffects(unisonEvents);

            // We subscribe here since there would be no point if there
            // aren't any unison phrases in the song
            GameManager.OnUnisonPhraseSuccess += OnUnisonPhraseSuccess;
            // May as well unsubscribe now that the work is done
            GameManager.OnUnisonPhrasesReady -= OnUnisonPhrasesReady;
        }

        protected virtual void OnUnisonPhraseSuccess()
        {
            // TODO: Signal the engine to award an extra unit of SP
            YargLogger.LogDebug("TrackPlayer would award unison bonus if it knew how!");
        }

        protected virtual void OnCountdownChange(int measuresLeft, double countdownLength, double endTime)
        {
            TrackView.UpdateCountdown(measuresLeft, countdownLength, endTime);
        }

        protected virtual void OnStarPowerPhraseHit(TNote note)
        {
            GameManager.StarPowerPhraseHit(this, note.Time);
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

        private void MergeTrackEffects(List<GameManager.UnisonEvent> unisonEvents)
        {
            // TODO: There is definitely a better algorithm to deal with this
            //  also it doesn't handle the case where multiple unisons exist
            //  within a single solo. I think it should work enough of the time
            //  to be testable, though.
            var soloIdx = 0;
            var unisonIdx = 0;
            var soloEvents = _upcomingSolos.ToList();

            while (soloIdx < soloEvents.Count || unisonIdx < unisonEvents.Count)
            {
                if (soloIdx >= _upcomingSolos.Count)
                {
                    // We ran out of solos, so stuff the unisons on the end
                    var lastSoloEndTime = 0.0;
                    if (soloEvents.Count > 0)
                    {
                        // There were solos, so we are safe to do this
                        lastSoloEndTime = soloEvents[^1].EndTime;
                    }
                    if (unisonEvents[unisonIdx].Time > lastSoloEndTime)
                    {
                        _trackEffectList.Add(new TrackEffect(unisonEvents[unisonIdx].Time,
                            unisonEvents[unisonIdx].TimeEnd, TrackEffectType.Unison));
                    }
                    else
                    {
                        // TODO: Deal with overlaps
                    }

                    unisonIdx++;
                    continue;
                }

                if (unisonIdx >= unisonEvents.Count)
                {
                    // We ran out of unisons, so stuff the solos on the end
                    var lastUnisonEndTime = 0.0;
                    if (unisonEvents.Count > 0)
                    {
                        // There were unisons, so we're safe to do this
                        lastUnisonEndTime = unisonEvents[^1].TimeEnd;
                    }
                    if (soloEvents[soloIdx].StartTime > lastUnisonEndTime)
                    {
                        _trackEffectList.Add(new TrackEffect(soloEvents[soloIdx].StartTime,
                            soloEvents[soloIdx].EndTime, TrackEffectType.Solo));
                    }
                    else
                    {
                        // TODO: Deal with overlaps
                    }

                    soloIdx++;
                    continue;
                }

                // TODO: Handle the case where start times are equal
                if (soloEvents[soloIdx].StartTime < unisonEvents[unisonIdx].Time)
                {
                    // A solo event is first, now we have to see if there is overlap
                    if (soloEvents[soloIdx].EndTime > unisonEvents[unisonIdx].Time)
                    {
                        // Create a solo effect lasting until the unison starts
                        var newSoloEndTime = unisonEvents[unisonIdx].Time;
                        _trackEffectList.Add(new TrackEffect(soloEvents[soloIdx].StartTime,
                            newSoloEndTime, TrackEffectType.Solo, true, false));
                        // Check if the unison ends before the solo ends
                        if (unisonEvents[unisonIdx].TimeEnd <= soloEvents[soloIdx].EndTime)
                        {
                            // unison is fully contained within the solo section
                            // TODO: This will not work right if the last unison in a solo extends past the end of the solo
                            while (unisonEvents[unisonIdx].TimeEnd < soloEvents[soloIdx].EndTime)
                            {
                                // We're looping to handle the case where there is more than one unison in a single solo

                                // create a soloandunison effect lasting until the unison ends
                                _trackEffectList.Add(new TrackEffect(unisonEvents[unisonIdx].Time,
                                    unisonEvents[unisonIdx].TimeEnd, TrackEffectType.SoloAndUnison, false, false));
                                // now create a solo effect lasting until the end of the original solo section
                                // or the start of the next unison section, if the next one is still within this solo
                                if (unisonIdx + 1 < unisonEvents.Count && unisonEvents[unisonIdx + 1].Time <= soloEvents[soloIdx].EndTime)
                                {
                                    _trackEffectList.Add(new TrackEffect(unisonEvents[unisonIdx].TimeEnd,
                                        unisonEvents[unisonIdx + 1].Time, TrackEffectType.Solo, false, false));
                                }
                                else
                                {
                                    _trackEffectList.Add(new TrackEffect(unisonEvents[unisonIdx].TimeEnd,
                                        soloEvents[soloIdx].EndTime, TrackEffectType.Solo, false, true));
                                }

                                unisonIdx++;
                                // If we ate the last unison, bad things will happen, so check for that
                                if (unisonIdx >= unisonEvents.Count)
                                {
                                    break;
                                }
                            }
                        }
                        else
                        {
                            // Solo ends before unison
                            _trackEffectList.Add(new TrackEffect(unisonEvents[unisonIdx].Time,
                                soloEvents[soloIdx].EndTime, TrackEffectType.SoloAndUnison, true, false));
                            // Finish unison event without solo
                            _trackEffectList.Add(new TrackEffect(soloEvents[soloIdx].EndTime,
                                unisonEvents[unisonIdx].TimeEnd, TrackEffectType.Unison, false, true));
                            unisonIdx++;
                        }
                        // We ate a solo and a unison and are done with this round
                        soloIdx++;
                        continue;
                    }

                    _trackEffectList.Add(new TrackEffect(soloEvents[soloIdx].StartTime,
                        soloEvents[soloIdx].EndTime, TrackEffectType.Solo, true, true));

                    soloIdx++;
                    continue;
                }

                if (unisonEvents[unisonIdx].Time <= soloEvents[soloIdx].StartTime)
                {
                    if (unisonEvents[unisonIdx].TimeEnd > soloEvents[soloIdx].StartTime)
                    {
                        var newUnisonEndTime = soloEvents[soloIdx].StartTime;
                        // Create the effect for the part of the unison without a solo
                        _trackEffectList.Add(new TrackEffect(unisonEvents[unisonIdx].Time,
                            newUnisonEndTime, TrackEffectType.Unison, true, false));
                        // Check if the solo is completely contained within the unison
                        if (soloEvents[soloIdx].EndTime < unisonEvents[unisonIdx].TimeEnd)
                        {
                            while (soloEvents[soloIdx].EndTime < unisonEvents[unisonIdx].TimeEnd)
                            {
                                // I seriously doubt this will ever happen, but we loop just in case there does
                                // ever exist a situation where there are multiple solos within a unison

                                // create soloandunison until the solo ends
                                _trackEffectList.Add(new TrackEffect(soloEvents[soloIdx].StartTime,
                                    soloEvents[soloIdx].EndTime, TrackEffectType.SoloAndUnison, false, false));
                                // finish off with the rest of the unison unless the unison contains another solo
                                if (soloIdx + 1 < soloEvents.Count && soloEvents[soloIdx + 1].StartTime <= unisonEvents[unisonIdx].TimeEnd)
                                {
                                    _trackEffectList.Add(new TrackEffect(soloEvents[soloIdx].EndTime,
                                        soloEvents[soloIdx + 1].StartTime, TrackEffectType.Unison, false, false));
                                }
                                else
                                {
                                    _trackEffectList.Add(new TrackEffect(soloEvents[soloIdx].EndTime,
                                        unisonEvents[unisonIdx].TimeEnd, TrackEffectType.Unison, false, true));
                                }

                                soloIdx++;
                                if (soloIdx >= soloEvents.Count)
                                {
                                    break;
                                }
                            }
                        }
                        else
                        {
                            // unison ends before solo
                            // create soloandunison until the unison ends
                            _trackEffectList.Add(new TrackEffect(soloEvents[soloIdx].StartTime,
                                unisonEvents[unisonIdx].TimeEnd, TrackEffectType.SoloAndUnison, true, false));
                            // finish off with the rest of the solo
                            _trackEffectList.Add(new TrackEffect(unisonEvents[unisonIdx].TimeEnd,
                                soloEvents[soloIdx].EndTime, TrackEffectType.Solo, false, true));
                            soloIdx++;
                        }
                        // We ate both a solo and a unison here
                        unisonIdx++;
                        continue;
                    }

                    _trackEffectList.Add(new TrackEffect(unisonEvents[unisonIdx].Time,
                        unisonEvents[unisonIdx].TimeEnd, TrackEffectType.Unison, true, true));

                    unisonIdx++;
                    continue;
                }
            }
            // Clear the effects queue that right now contains only solos
            _upcomingEffects.Clear();
            // Add the merged effects to the queue
            foreach (var t in _trackEffectList)
            {
                _upcomingEffects.Enqueue(t);
            }
            YargLogger.LogFormatDebug("Created {0} track effects in MergeTrackEffects", _upcomingEffects.Count);
        }
    }
}