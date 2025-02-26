﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using YARG.Audio;
using YARG.Core;
using YARG.Core.Audio;
using YARG.Core.Chart;
using YARG.Core.Engine.Guitar;
using YARG.Core.Engine.Guitar.Engines;
using YARG.Core.Game;
using YARG.Core.Input;
using YARG.Core.Logging;
using YARG.Core.Replays;
using YARG.Gameplay.HUD;
using YARG.Gameplay.Visuals;
using YARG.Helpers;
using YARG.Player;
using YARG.Settings;
using Random = UnityEngine.Random;

namespace YARG.Gameplay.Player
{
    public sealed class FiveFretPlayer : TrackPlayer<GuitarEngine, GuitarNote>
    {
        private const double SUSTAIN_END_MUTE_THRESHOLD      = 0.1;

        private const int   SHIFT_INDICATOR_MEASURES_BEFORE  = 5;

        public override bool ShouldUpdateInputsOnResume => true;

        private static float[] GuitarStarMultiplierThresholds => new[]
        {
            0.21f, 0.46f, 0.77f, 1.85f, 3.08f, 4.52f
        };

        private static float[] BassStarMultiplierThresholds => new[]
        {
            0.21f, 0.50f, 0.90f, 2.77f, 4.62f, 6.78f
        };

        public GuitarEngineParameters EngineParams { get; private set; }

        private double TimeFromSpawnToStrikeline => SpawnTimeOffset - (-STRIKE_LINE_POS / NoteSpeed);

        public struct RangeShiftIndicator
        {
            public double Time;
            public bool   LeftSide;
            public int    Offset;
            public bool   RangeIndicator;
        }

        private List<FiveFretRangeShift>   _allRangeShiftEvents  = new();
        private Queue<FiveFretRangeShift>  _rangeShiftEventQueue = new();
        private FiveFretRangeShift         CurrentRange { get; set; }
        private Queue<RangeShiftIndicator> _shiftIndicators = new();
        private int                        _shiftIndicatorIndex;
        private bool                       _fretPulseStarting;
        private double                     _fretPulseStartTime;

        private bool[] _activeFrets;

        [Header("Five Fret Specific")]
        [SerializeField]
        private FretArray _fretArray;
        [SerializeField]
        private Pool _shiftIndicatorPool;
        [SerializeField]
        private Pool _rangeIndicatorPool;

        public override float[] StarMultiplierThresholds { get; protected set; } =
            GuitarStarMultiplierThresholds;

        public override int[] StarScoreThresholds { get; protected set; }

        public float WhammyFactor { get; private set; }

        private int _sustainCount;

        private SongStem _stem;
        private double   _practiceSectionStartTime;

        public override void Initialize(int index, YargPlayer player, SongChart chart, TrackView trackView, StemMixer mixer, int? currentHighScore)
        {
            _stem = player.Profile.CurrentInstrument.ToSongStem();
            if (_stem == SongStem.Bass && mixer[SongStem.Bass] == null)
            {
                _stem = SongStem.Rhythm;
            }
            base.Initialize(index, player, chart, trackView, mixer, currentHighScore);
        }

        protected override InstrumentDifficulty<GuitarNote> GetNotes(SongChart chart)
        {
            var track = chart.GetFiveFretTrack(Player.Profile.CurrentInstrument).Clone();
            return track.GetDifficulty(Player.Profile.CurrentDifficulty);
        }

        protected override GuitarEngine CreateEngine()
        {
            // If on bass, replace the star multiplier threshold
            bool isBass = Player.Profile.CurrentInstrument == Instrument.FiveFretBass;
            if (isBass)
            {
                StarMultiplierThresholds = BassStarMultiplierThresholds;
            }

            if (GameManager.ReplayInfo == null)
            {
                // Create the engine params from the engine preset
                EngineParams = Player.EnginePreset.FiveFretGuitar.Create(StarMultiplierThresholds, isBass);
                //EngineParams = EnginePreset.Precision.FiveFretGuitar.Create(StarMultiplierThresholds, isBass);
            }
            else
            {
                // Otherwise, get from the replay
                EngineParams = (GuitarEngineParameters) Player.EngineParameterOverride;
            }

            var engine = new YargFiveFretEngine(NoteTrack, SyncTrack, EngineParams, Player.Profile.IsBot);

            HitWindow = EngineParams.HitWindow;

            YargLogger.LogFormatDebug("Note count: {0}", NoteTrack.Notes.Count);

            engine.OnNoteHit += OnNoteHit;
            engine.OnNoteMissed += OnNoteMissed;
            engine.OnOverstrum += OnOverhit;

            engine.OnSustainStart += OnSustainStart;
            engine.OnSustainEnd += OnSustainEnd;

            engine.OnSoloStart += OnSoloStart;
            engine.OnSoloEnd += OnSoloEnd;

            engine.OnStarPowerPhraseHit += OnStarPowerPhraseHit;
            engine.OnStarPowerStatus += OnStarPowerStatus;

            engine.OnCountdownChange += OnCountdownChange;

            return engine;
        }

        protected override void FinishInitialization()
        {
            base.FinishInitialization();

            StarScoreThresholds = PopulateStarScoreThresholds(StarMultiplierThresholds, Engine.BaseScore);

            IndicatorStripes.Initialize(Player.EnginePreset.FiveFretGuitar);
            _fretArray.Initialize(
                Player.ThemePreset,
                Player.Profile.GameMode,
                Player.ColorProfile.FiveFretGuitar,
                Player.Profile.LeftyFlip);

            InitializeRangeShift();
            GameManager.BeatEventHandler.Subscribe(_fretArray.PulseFretColors);
        }

        public override void ResetPracticeSection()
        {
            base.ResetPracticeSection();
            ResetRangeShift(_practiceSectionStartTime);

            _fretArray.ResetAll();
        }

        public override void SetPracticeSection(uint start, uint end)
        {
            base.SetPracticeSection(start, end);

            // This will set the current range correctly
            _practiceSectionStartTime = SyncTrack.TickToTime(start);
            InitializeRangeShift(SyncTrack.TickToTime(start));
        }

        public override void SetReplayTime(double time)
        {
            ResetRangeShift(time);
            base.SetReplayTime(time);
        }

        private void ResetRangeShift(double time)
        {
            // Despawn shift indicators and rebuild the shift queues based on the replay time
            _rangeShiftEventQueue.Clear();
            _shiftIndicators.Clear();
            for (var i = 0; i < _shiftIndicatorPool.AllSpawned.Count; i++)
            {
                var poolable = _shiftIndicatorPool.AllSpawned[i];
                _shiftIndicatorPool.Return(poolable);
            }

            for (var i = 0; i < _rangeIndicatorPool.AllSpawned.Count; i++)
            {
                var poolable = _rangeIndicatorPool.AllSpawned[i];
                _rangeIndicatorPool.Return(poolable);
            }

            InitializeRangeShift(time);
        }

        protected override void UpdateVisuals(double songTime)
        {
            UpdateBaseVisuals(Engine.EngineStats, EngineParams, songTime);

            UpdateRangeShift(songTime);

            for (var fret = GuitarAction.GreenFret; fret <= GuitarAction.OrangeFret; fret++)
            {
                _fretArray.SetPressed((int) fret, Engine.IsFretHeld(fret));
            }
        }

        public void UpdateRangeShift(double songTime)
        {
            if (!_rangeShiftEventQueue.TryPeek(out var nextShift))
            {
                return;
            }

            if (_shiftIndicators.TryPeek(out var shiftIndicator) && shiftIndicator.Time <= songTime + SpawnTimeOffset)
            {
                // The range indicator is dealt with in its own function
                if (shiftIndicator.RangeIndicator)
                {
                    SpawnRangeIndicator(nextShift);
                    return;
                }
                if (!_shiftIndicatorPool.CanSpawnAmount(1))
                {
                    return;
                }

                var poolable = _shiftIndicatorPool.TakeWithoutEnabling();
                if (poolable == null)
                {
                    YargLogger.LogWarning("Attempted to spawn shift indicator, but it's at its cap!");
                    return;
                }

                YargLogger.LogDebug("Shift indicator spawned!");

                ((GuitarShiftIndicatorElement) poolable).RangeShiftIndicator = shiftIndicator;
                poolable.EnableFromPool();

                _shiftIndicators.Dequeue();

                if (!_fretPulseStarting)
                {
                    _fretPulseStarting = true;
                    _fretPulseStartTime = nextShift.Time - (nextShift.BeatDuration * SHIFT_INDICATOR_MEASURES_BEFORE);
                }
            }

            if (_fretPulseStarting && _fretPulseStartTime <= songTime)
            {
                for (var i = nextShift.Range - 1; i < nextShift.Range + nextShift.Size - 1; i++)
                {
                    _fretArray.SetFretColorPulse(i, true, (float) nextShift.BeatDuration);
                }

                _fretPulseStarting = false;
            }


            // Turn off the pulsing and switch active frets now that we're in the new range
            if (nextShift.Time <= songTime)
            {
                _rangeShiftEventQueue.Dequeue();
                for (var i = 0; i < _fretArray.FretCount; i++)
                {
                    _fretArray.SetFretColorPulse(i, false, (float) nextShift.BeatDuration);
                }

                _fretPulseStarting = false;
                CurrentRange = nextShift;
                SetActiveFretsForShiftEvent(nextShift);
            }
        }

        private void SpawnRangeIndicator(FiveFretRangeShift nextShift)
        {
            if (!_rangeIndicatorPool.CanSpawnAmount(1))
            {
                return;
            }

            var poolable = _rangeIndicatorPool.TakeWithoutEnabling();
            if (poolable == null)
            {
                YargLogger.LogWarning("Attempted to spawn range indicator, but it's at its cap!");
                return;
            }

            YargLogger.LogDebug("Range indicator spawned!");

            ((GuitarRangeIndicatorElement) poolable).RangeShift = nextShift;
            poolable.EnableFromPool();

            _shiftIndicators.Dequeue();
        }

        public override void SetStemMuteState(bool muted)
        {
            if (IsStemMuted != muted)
            {
                GameManager.ChangeStemMuteState(_stem, muted);
                IsStemMuted = muted;
            }
        }

        public override void SetStarPowerFX(bool active)
        {
            GameManager.ChangeStemReverbState(_stem, active);
        }

        protected override void ResetVisuals()
        {
            base.ResetVisuals();

            _fretArray.ResetAll();
        }

        protected override void InitializeSpawnedNote(IPoolable poolable, GuitarNote note)
        {
            ((FiveFretNoteElement) poolable).NoteRef = note;
        }

        protected override void OnNoteHit(int index, GuitarNote chordParent)
        {
            base.OnNoteHit(index, chordParent);

            if (GameManager.Paused) return;

            foreach (var note in chordParent.AllNotes)
            {
                (NotePool.GetByKey(note) as FiveFretNoteElement)?.HitNote();

                if (note.Fret != (int) FiveFretGuitarFret.Open)
                {
                    _fretArray.PlayHitAnimation(note.Fret - 1);
                }
                else
                {
                    _fretArray.PlayOpenHitAnimation();
                }
            }
        }

        protected override void OnNoteMissed(int index, GuitarNote chordParent)
        {
            base.OnNoteMissed(index, chordParent);

            foreach (var note in chordParent.AllNotes)
            {
                (NotePool.GetByKey(note) as FiveFretNoteElement)?.MissNote();
            }
        }

        protected override void OnOverhit()
        {
            base.OnOverhit();

            if (GameManager.IsSeekingReplay)
            {
                return;
            }

            if (SettingsManager.Settings.OverstrumAndOverhitSoundEffects.Value)
            {
                const int MIN = (int) SfxSample.Overstrum1;
                const int MAX = (int) SfxSample.Overstrum4;

                var randomOverstrum = (SfxSample) Random.Range(MIN, MAX + 1);
                GlobalAudioHandler.PlaySoundEffect(randomOverstrum);
            }
        }

        private void OnSustainStart(GuitarNote parent)
        {
            foreach (var note in parent.AllNotes)
            {
                // If the note is disjoint, only iterate the parent as sustains are added separately
                if (parent.IsDisjoint && parent != note)
                {
                    continue;
                }

                if (note.Fret != (int) FiveFretGuitarFret.Open)
                {
                    _fretArray.SetSustained(note.Fret - 1, true);
                }

                _sustainCount++;
            }
        }

        private void OnSustainEnd(GuitarNote parent, double timeEnded, bool finished)
        {
            foreach (var note in parent.AllNotes)
            {
                // If the note is disjoint, only iterate the parent as sustains are added separately
                if (parent.IsDisjoint && parent != note)
                {
                    continue;
                }

                (NotePool.GetByKey(note) as FiveFretNoteElement)?.SustainEnd(finished);

                if (note.Fret != (int) FiveFretGuitarFret.Open)
                {
                    _fretArray.SetSustained(note.Fret - 1, false);
                }

                _sustainCount--;
            }

            // Mute the stem if you let go of the sustain too early.
            // Leniency is handled by the engine's sustain burst threshold.
            if (!finished)
            {
                if (!parent.IsDisjoint || _sustainCount == 0)
                {
                    SetStemMuteState(true);
                }
            }

            if (_sustainCount == 0)
            {
                WhammyFactor = 0;
                GameManager.ChangeStemWhammyPitch(_stem, 0);
            }
        }

        protected override bool InterceptInput(ref GameInput input)
        {
            // Ignore SP in practice mode
            if (input.GetAction<GuitarAction>() == GuitarAction.StarPower && GameManager.IsPractice) return true;

            return false;
        }

        protected override void OnInputQueued(GameInput input)
        {
            base.OnInputQueued(input);

            // Update the whammy factor
            if (_sustainCount > 0 && input.GetAction<GuitarAction>() == GuitarAction.Whammy)
            {
                WhammyFactor = Mathf.Clamp01(input.Axis);
                GameManager.ChangeStemWhammyPitch(_stem, WhammyFactor);
            }
        }

        public override (ReplayFrame Frame, ReplayStats Stats) ConstructReplayData()
        {
            var frame = new ReplayFrame(Player.Profile, EngineParams, Engine.EngineStats, ReplayInputs.ToArray());
            return (frame, Engine.EngineStats.ConstructReplayStats(Player.Profile.Name));
        }


        private void InitializeRangeShift(double time = 0)
        {
            _rangeShiftEventQueue.Clear();
            // Default to everything on
            _activeFrets = new bool[_fretArray.FretCount];
            for (int i = 0; i < _fretArray.FretCount; i++)
            {
                _activeFrets[i] = true;
            }

            if (_allRangeShiftEvents.Count == 0)
            {
                // Since we don't have a list yet, get it
                _allRangeShiftEvents =
                    FiveFretRangeShift.GetRangeShiftEvents(NoteTrack.TextEvents, NoteTrack.Difficulty);
            }

            // Fewer than two range shifts makes no sense
            if (_allRangeShiftEvents.Count < 1)
            {
                // _rangeShiftEventQueue = new Queue<FiveFretRangeShift>();
                return;
            }

            if (_allRangeShiftEvents.Count == 1)
            {
                // There are no actual shifts, but we should dim unused frets
                SetActiveFretsForShiftEvent(_allRangeShiftEvents[0]);
                CurrentRange = _allRangeShiftEvents[0];
                // _rangeShiftEventQueue = new Queue<FiveFretRangeShift>();
                return;
            }

            // Turns out that we have range shifts that need indicators
            var firstEvent = _allRangeShiftEvents[0];
            _rangeShiftEventQueue = new Queue<FiveFretRangeShift>();

            FiveFretRangeShift mostRecentEvent = firstEvent;

            // Only queue range shifts that happen after time
            foreach (var e in _allRangeShiftEvents)
            {
                // The first event just sets the starting range, so skip it
                if (_allRangeShiftEvents.IndexOf(e) == 0)
                {
                    continue;
                }
                // These have no visible effect on the track, so we just
                // want to make sure any that are current or in the future are queued
                // and to figure out which was the most recent event
                if (e.Time >= time)
                {
                    _rangeShiftEventQueue.Enqueue(e);
                    continue;
                }

                if (e.Time > mostRecentEvent.Time)
                {
                    mostRecentEvent = e;
                }
            }

            CurrentRange = mostRecentEvent;
            SetActiveFretsForShiftEvent(CurrentRange);

            // Figure out where the indicators should go
            var beatlines = Beatlines
                .Where(i => i.Type is BeatlineType.Measure or BeatlineType.Strong)
                .ToList();

            _shiftIndicators.Clear();
            int lastShiftRange = mostRecentEvent.Range;
            int beatlineIndex = 0;

            foreach (var shift in _rangeShiftEventQueue.ToList())
            {
                if (shift.Range == lastShiftRange)
                {
                    continue;
                }

                var shiftLeft = Player.Profile.LeftyFlip ? shift.Range < lastShiftRange : shift.Range > lastShiftRange;
                lastShiftRange = shift.Range;

                double lastBeatTime = 0;
                double firstBeatTime = double.MaxValue;

                // Find the first beatline index after the range shift
                for (; beatlineIndex < beatlines.Count; beatlineIndex++)
                {
                    if (beatlines[beatlineIndex].Time > shift.Time)
                    {
                        lastBeatTime = beatlines[beatlineIndex].Time;
                        break;
                    }
                }

                // Add the indicators before the range shift
                // While we're doing this, figure out the time between beats
                for (int i = SHIFT_INDICATOR_MEASURES_BEFORE; i > 0; i--)
                {
                    var realIndex = beatlineIndex - i;

                    // If the indicator is before any measures, skip
                    if (realIndex < 0)
                    {
                        break;
                    }

                    firstBeatTime = beatlines[realIndex].Time < firstBeatTime ? beatlines[realIndex].Time : firstBeatTime;

                    _shiftIndicators.Enqueue(new RangeShiftIndicator
                    {
                        Time = beatlines[realIndex].Time,
                        LeftSide = shiftLeft,
                        Offset = shiftLeft ? ((shift.Range + shift.Size) - 6) * -1 : shift.Range - 1,
                        RangeIndicator = i == 1,
                    });
                }

                // In case we have no samples for this shift event, 0.5 is a reasonable default
                shift.BeatDuration = firstBeatTime < double.MaxValue ? (lastBeatTime - firstBeatTime) / SHIFT_INDICATOR_MEASURES_BEFORE : 0.5;
            }
        }

        private void SetActiveFretsForShiftEvent(FiveFretRangeShift range)
        {
            bool[] newFrets = new bool[5];

            int start = range.Range - 1;
            int end = start + range.Size;
            for (int i = start; i < end; i++)
            {
                newFrets[i] = true;
            }

            if (!newFrets.SequenceEqual(_activeFrets))
            {
                _activeFrets = newFrets;
                _fretArray.UpdateFretActiveState(_activeFrets);
            }
        }
    }
}