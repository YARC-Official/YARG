using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using YARG.Core;
using YARG.Core.Audio;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Core.Engine.Guitar;
using YARG.Core.Engine.Guitar.Engines;
using YARG.Core.Input;
using YARG.Core.Logging;
using YARG.Core.Replays;
using YARG.Gameplay.HUD;
using YARG.Gameplay.Visuals;
using YARG.Helpers;
using YARG.Helpers.Extensions;
using YARG.Playback;
using YARG.Player;
using YARG.Settings;
using YARG.Themes;
using static YARG.Core.Game.ColorProfile;
using Random = UnityEngine.Random;

namespace YARG.Gameplay.Player
{
    public sealed class FiveFretGuitarPlayer : TrackPlayer<GuitarEngine, GuitarNote>
    {
        private const double SUSTAIN_END_MUTE_THRESHOLD = 0.1;

        private const int SHIFT_INDICATOR_MEASURES_BEFORE = 5;

        public const int LANE_COUNT = 5;

        // Key is a FiveFretGuitarFret
        // Value is the fret's lateral position on the fret array
        private Dictionary<int, int> _lanePositions;

        private float GetLanePositionOrCentered(int fret)
        {
            if (_lanePositions.ContainsKey(fret))
            {
                return _lanePositions[fret];
            }

            return (LANE_COUNT - 1) / 2;
        }

        private FiveFretGuitarFret GetFretIndex(GuitarAction action)
        {
            return action switch
            {
                GuitarAction.Fret1 => FiveFretGuitarFret.Green,
                GuitarAction.Fret2 => FiveFretGuitarFret.Red,
                GuitarAction.Fret3 => FiveFretGuitarFret.Yellow,
                GuitarAction.Fret4 => FiveFretGuitarFret.Blue,
                GuitarAction.Fret5 => FiveFretGuitarFret.Orange,
                _ => throw new ArgumentOutOfRangeException(nameof(action))
            };
        }

        public int GetLanePosition(FiveFretGuitarFret fret)
        {
            return _lanePositions[(int)fret];
        }

        public static Dictionary<int, int> DEFAULT_HIGHWAY_ORDERING = new()
            {
                { (int)FiveFretGuitarFret.Green,     0 },
                { (int)FiveFretGuitarFret.Red,       1 },
                { (int)FiveFretGuitarFret.Yellow,    2 },
                { (int)FiveFretGuitarFret.Blue,      3 },
                { (int)FiveFretGuitarFret.Orange,    4 }
            };

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
            public bool RightSide;
            public int Offset;
            public bool RangeIndicator;
        }

        private FiveFretRangeShift[] _allRangeShiftEvents;
        private readonly Queue<FiveFretRangeShift> _rangeShiftEventQueue = new();
        private FiveFretRangeShift CurrentRange { get; set; }
        private readonly Queue<RangeShiftIndicator> _shiftIndicators = new();
        private int _shiftIndicatorIndex;
        private bool _fretPulseStarting;
        private double _fretPulseStartTime;

        private List<int> _activeFrets = new(5);

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
        private double _practiceSectionStartTime;

        public override void Initialize(int index, YargPlayer player, SongChart chart, TrackView trackView, StemMixer mixer, int? currentHighScore)
        {
            _stem = player.Profile.CurrentInstrument.ToSongStem();
            if (_stem == SongStem.Bass && mixer[SongStem.Bass] == null)
            {
                _stem = SongStem.Rhythm;
            }

            BRELanes = new LaneElement[LANE_COUNT];
            LaneCount = LANE_COUNT;

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

            if (!Player.IsReplay)
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

            if (EngineContainer != null)
            {
                GameManager.EngineManager.Unregister(EngineContainer);
                EngineContainer = null;
            }

            var engine = new YargFiveFretGuitarEngine(NoteTrack, SyncTrack, EngineParams, Player.Profile.IsBot);
            EngineContainer = GameManager.EngineManager.Register(engine, NoteTrack.Instrument, Chart, Player.RockMeterPreset);

            HitWindow = EngineParams.HitWindow;

            YargLogger.LogFormatDebug("Note count: {0}", NoteTrack.Notes.Count);

            engine.OnNoteHit += OnNoteHit;
            engine.OnNoteMissed += OnNoteMissed;
            engine.OnOverstrum += OnOverhit;

            engine.OnSustainStart += OnSustainStart;
            engine.OnSustainEnd += OnSustainEnd;

            engine.OnSoloStart += OnSoloStart;
            engine.OnSoloEnd += OnSoloEnd;

            engine.OnCodaStart += OnCodaStart;
            engine.OnCodaEnd += OnCodaEnd;

            engine.OnStarPowerPhraseHit += OnStarPowerPhraseHit;
            engine.OnStarPowerPhraseMissed += OnStarPowerPhraseMissed;
            engine.OnStarPowerStatus += OnStarPowerStatus;

            engine.OnCountdownChange += OnCountdownChange;

            return engine;
        }

        protected override void FinishInitialization()
        {
            base.FinishInitialization();

            MakeHighwayOrdering();

            StarScoreThresholds = PopulateStarScoreThresholds(StarMultiplierThresholds, Engine.BaseScore);

            IndicatorStripes.Initialize(Player.EnginePreset.FiveFretGuitar);


            _fretArray.Initialize(
                _lanePositions,
                LANE_COUNT,
                null,
                Player.ColorProfile.FiveFretGuitar,
                Player.ThemePreset,
                VisualStyle.FiveFretGuitar
            );

            if (Player.Profile.RangeEnabled)
            {
                _allRangeShiftEvents = FiveFretRangeShift.GetRangeShiftEvents(NoteTrack);
                InitializeRangeShift();
            }

            LaneElement.DefineLaneScale(Player.Profile.CurrentInstrument, 5);

            GameManager.BeatEventHandler.Visual.Subscribe(_fretArray.PulseFretColors, BeatEventType.StrongBeat);
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
            ResetRangeShift(_practiceSectionStartTime);
        }

        public override void SetReplayTime(double time)
        {
            ResetRangeShift(time);
            base.SetReplayTime(time);
        }

        protected override void UpdateVisuals(double visualTime)
        {
            // Update coda lane emissions if necessary
            if (Engine.IsCodaActive)
            {
                // Set emission color of BRE lanes depending on currently available score value
                for (int i = 0; i < CurrentCoda.ScoringZones; i++)
                {
                    // var intensity = CurrentCoda.GetNormalizedTimeSinceLastHit(i, visualTime);
                    // intensity = (float) Math.Clamp(Math.Cos(Math.PI * intensity), 0f, 1f);
                    // intensity = (float) Math.Clamp((Math.Tan(intensity) * -1) + 1, 0f, 1f);
                    // intensity = (float) Math.Clamp((Math.Atan(intensity * 3) * -1.8) + 2, 0f, 2f);
                    // intensity = 1 - Mathf.Sin(Mathf.Pow(intensity, 1f / 2.4f) * 2);
                    // intensity = 1 - Mathf.Sin(Mathf.Pow(intensity, 0.5f) * 1.6f);
                    BRELanes[i].SetEmissionColor(CurrentCoda.GetNormalizedTimeSinceLastHit(i, visualTime));
                }
            }

            base.UpdateVisuals(visualTime);
            UpdateRangeShift(visualTime);
            UpdateFretArray();
        }

        public void UpdateRangeShift(double visualTime)
        {
            if (!_rangeShiftEventQueue.TryPeek(out var nextShift))
            {
                return;
            }

            if (_shiftIndicators.TryPeek(out var shiftIndicator) && shiftIndicator.Time <= visualTime + SpawnTimeOffset)
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

            if (_fretPulseStarting && _fretPulseStartTime <= visualTime)
            {
                for (var i = nextShift.Position; i < nextShift.Position + nextShift.Size; i++)
                {
                    _fretArray.SetFretColorPulse(i, true, (float) nextShift.BeatDuration);
                }

                _fretPulseStarting = false;
            }


            // Turn off the pulsing and switch active frets now that we're in the new range
            if (nextShift.Time <= visualTime)
            {
                _rangeShiftEventQueue.Dequeue();
                foreach (var fretIndex in _lanePositions.Keys)
                {
                    _fretArray.SetFretColorPulse(fretIndex, false, (float) nextShift.BeatDuration);
                }

                _fretPulseStarting = false;
                CurrentRange = nextShift;
                SetActiveFretsForShiftEvent(nextShift);
            }
        }

        private void ResetRangeShift(double time)
        {
            if (!Player.Profile.RangeEnabled)
            {
                return;
            }

            // Despawn shift indicators and rebuild the shift queues based on the replay time
            _rangeShiftEventQueue.Clear();
            _shiftIndicators.Clear();
            _shiftIndicatorPool.ReturnAllObjects();
            _rangeIndicatorPool.ReturnAllObjects();
            InitializeRangeShift(time);

        }

        private void UpdateFretArray()
        {
            for (var action = GuitarAction.GreenFret; action <= GuitarAction.OrangeFret; action++)
            {
                _fretArray.SetPressed((int)GetFretIndex(action), Engine.IsFretHeld(action));
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
            ((FiveFretGuitarNoteElement) poolable).NoteRef = note;
        }

        protected override void InitializeSpawnedLane(LaneElement lane, GuitarNote note)
        {
            lane.SetAppearance(
                Player.Profile.CurrentInstrument,
                note.LaneNote,
                GetLanePositionOrCentered(note.Fret),
                LANE_COUNT,
                Player.ColorProfile.FiveFretGuitar.GetNoteColor(note.Fret).ToUnityColor()
            );
        }

        protected override void InitializeSpawnedLane(LaneElement lane, int laneIndex)
        {
            var index = Player.Profile.LeftyFlip ? (LANE_COUNT - 1) - laneIndex : laneIndex;
            lane.SetAppearance(
                Player.Profile.CurrentInstrument,
                laneIndex,
                laneIndex,
                LANE_COUNT,
                Player.ColorProfile.FiveFretGuitar.GetNoteColor(index + 1).ToUnityColor());
        }

        protected override void ModifyLaneFromNote(LaneElement lane, GuitarNote note)
        {
            if (note.Fret == (int) FiveFretGuitarFret.Open)
            {
                lane.ToggleOpen(true);
            }
            else
            {
                lane.MultiplyScale(0.85f);
            }
        }

        protected override void RescaleLanesForBRE()
        {
            LaneElement.DefineLaneScale(Player.Profile.CurrentInstrument, 5, true);
        }

        private void OnLaneHit(int fret)
        {
            _fretArray.PlayCodaHitAnimation(fret + 1);
        }

        protected override void OnCodaStart(CodaSection coda)
        {
            base.OnCodaStart(coda);
            CurrentCoda.OnLaneHit += OnLaneHit;

            _fretArray.SetBreMode(true);
        }

        protected override void OnCodaEnd(CodaSection coda)
        {
            base.OnCodaEnd(coda);
            CurrentCoda.OnLaneHit -= OnLaneHit;

            _fretArray.SetBreMode(false);
        }

        protected override void OnNoteHit(int index, GuitarNote chordParent)
        {
            base.OnNoteHit(index, chordParent);

            if (GameManager.Paused) return;

            foreach (var note in chordParent.AllNotes)
            {
                (NotePool.GetByKey(note) as FiveFretGuitarNoteElement)?.HitNote();

                if (note.Fret != (int) FiveFretGuitarFret.Open)
                {
                    _fretArray.PlayHitAnimation(note.Fret);
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
                (NotePool.GetByKey(note) as FiveFretGuitarNoteElement)?.MissNote();
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

            // To check if held frets are valid
            GuitarNote currentNote = null;
            if (Engine.NoteIndex < Notes.Count)
            {
                var note = Notes[Engine.NoteIndex];

                // Don't take the note if it's not within the hit window
                // TODO: Make BaseEngine.IsNoteInWindow public and use that instead
                var (frontEnd, backEnd) = Engine.CalculateHitWindow();
                if (Engine.CurrentTime >= (note.Time + frontEnd) && Engine.CurrentTime <= (note.Time + backEnd))
                {
                    currentNote = note;
                }
            }

            // Play miss animation for every held fret that does not match the current note
            bool anyHeld = false;
            for (var action = GuitarAction.GreenFret; action <= GuitarAction.OrangeFret; action++)
            {
                if (!Engine.IsFretHeld(action))
                {
                    continue;
                }

                anyHeld = true;

                if (currentNote == null || (currentNote.NoteMask & (1 << (int) action)) == 0)
                {
                    _fretArray.PlayMissAnimation((int) GetFretIndex(action));
                }
            }

            // Play open-strum miss if no frets are held
            if (!anyHeld)
            {
                _fretArray.PlayOpenMissAnimation();
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
                    _fretArray.SetSustained(note.Fret, true);
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

                (NotePool.GetByKey(note) as FiveFretGuitarNoteElement)?.SustainEnd(finished);

                if (note.Fret != (int) FiveFretGuitarFret.Open)
                {
                    _fretArray.SetSustained(note.Fret, false);
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

        protected override void OnStarPowerPhraseMissed()
        {
            base.OnStarPowerPhraseMissed();
            foreach (var note in NotePool.AllSpawned)
            {
                (note as FiveFretGuitarNoteElement)?.OnStarPowerUpdated();
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
            var firstShiftAfterFirstNote = false;
            _rangeShiftEventQueue.Clear();
            // Default to all frets on
            SetDefaultActiveFrets();

            // No range shifts, so just return
            if (_allRangeShiftEvents.Length < 1)
            {
                return;
            }

            // Now that we know there is at least one range shift, figure out if it is after the first note
            if (Notes.Count > 0 && _allRangeShiftEvents[0].Time > Notes[0].Time)
            {
                firstShiftAfterFirstNote = true;
            }

            if (_allRangeShiftEvents.Length == 1)
            {
                // There are no actual shifts (or we aren't shifting because of range compression), but we should dim unused frets
                CurrentRange = _allRangeShiftEvents[0];
                // If the range shift is after the first note, leave all the frets on because chart is broke
                if (!firstShiftAfterFirstNote)
                {
                    SetActiveFretsForShiftEvent(CurrentRange);
                }

                return;
            }

            // Turns out that we have range shifts that need indicators
            var firstEvent = _allRangeShiftEvents[0];

            FiveFretRangeShift mostRecentEvent = firstEvent;

            // Only queue range shifts that happen after time
            for (int i = 1; i < _allRangeShiftEvents.Length; i++)
            {
                FiveFretRangeShift e = _allRangeShiftEvents[i];
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
            if (time < mostRecentEvent.Time)
            {
                // If we get here, the only range shifts are in the future
                SetDefaultActiveFrets();
            }
            else
            {
                SetActiveFretsForShiftEvent(CurrentRange);
            }

            // Figure out where the indicators should go
            var beatlines = Beatlines
                .Where(i => i.Type is BeatlineType.Measure or BeatlineType.Strong)
                .ToList();

            _shiftIndicators.Clear();
            var lastShiftRange = mostRecentEvent;
            int beatlineIndex = 0;

            foreach (var shift in _rangeShiftEventQueue.ToList())
            {
                if (shift.Position == lastShiftRange.Position && shift.Size == lastShiftRange.Size)
                {
                    continue;
                }

                // When shift.Position and lastShiftRange.Position are the same, this result doesn't matter because
                // the shift indicator won't be displayed, so it's OK that neither of these are <= or >=
                var shiftRight = Player.Profile.LeftyFlip
                    ? shift.Position < lastShiftRange.Position
                    : shift.Position > lastShiftRange.Position;

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
                        RightSide = shiftRight,
                        Offset = shift.Position < lastShiftRange.Position ? shift.Position - 1 : 5 - shift.Position - shift.Size + 1,
                        RangeIndicator = i == 1 && !(shift.Position == lastShiftRange.Position && shift.Size == lastShiftRange.Size),
                    });
                }

                lastShiftRange = shift;

                // In case we have no samples for this shift event, 0.5 is a reasonable default
                shift.BeatDuration = firstBeatTime < double.MaxValue ? (lastBeatTime - firstBeatTime) / SHIFT_INDICATOR_MEASURES_BEFORE : 0.5;
            }
        }

        private void SetActiveFretsForShiftEvent(FiveFretRangeShift range)
        {
            var newFrets = new List<int>();

            int start = range.Position;
            int end = start + range.Size;
            for (int i = start; i < end; i++)
            {
                newFrets.Add(i);
            }

            if (!newFrets.SequenceEqual(_activeFrets))
            {
                _activeFrets = newFrets;
                _fretArray.UpdateFretActiveState(_activeFrets);
            }
        }

        private void SetDefaultActiveFrets()
        {
            var newFrets = new List<int>();
            foreach (var fretIdx in _lanePositions.Keys)
            {
                newFrets.Add(fretIdx);
            }

            if (!newFrets.SequenceEqual(_activeFrets))
            {
                _activeFrets = newFrets;
                _fretArray.UpdateFretActiveState(_activeFrets);
            }
        }

        private void MakeHighwayOrdering()
        {
            if (Player.Profile.LeftyFlip)
            {
                _lanePositions = new()
                {
                    { (int)FiveFretGuitarFret.Orange,    0 },
                    { (int)FiveFretGuitarFret.Blue,      1 },
                    { (int)FiveFretGuitarFret.Yellow,    2 },
                    { (int)FiveFretGuitarFret.Red,       3 },
                    { (int)FiveFretGuitarFret.Green,     4 }
                };
            } else
            {
                _lanePositions = DEFAULT_HIGHWAY_ORDERING;
            }
        }
    }
}