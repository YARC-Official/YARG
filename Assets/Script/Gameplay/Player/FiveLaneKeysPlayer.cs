using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using YARG.Core;
using YARG.Core.Audio;
using YARG.Core.Chart;
using YARG.Core.Engine.Keys;
using YARG.Core.Engine.Keys.Engines;
using YARG.Core.Engine;
using YARG.Core.Extensions;
using YARG.Core.Input;
using YARG.Core.Logging;
using YARG.Core.Replays;
using YARG.Gameplay;
using YARG.Gameplay.HUD;
using YARG.Gameplay.Player;
using YARG.Gameplay.Visuals;
using YARG.Helpers;
using YARG.Helpers.Extensions;
using YARG.Playback;
using YARG.Player;
using YARG.Themes;
using static YARG.Core.Engine.Keys.FiveLaneKeysEngine;

namespace YARG.Assets.Script.Gameplay.Player
{
    public sealed class FiveLaneKeysPlayer : TrackPlayer<FiveLaneKeysEngine, GuitarNote>
    {
        private const double SUSTAIN_END_MUTE_THRESHOLD = 0.1;

        private const int SHIFT_INDICATOR_MEASURES_BEFORE = 5;

        // Key is a FiveFretGuitarFret
        // Value is the fret's lateral position on the fret array
        private Dictionary<int, int> _lanePositions;

        private float GetLanePositionOrCentered(int fret)
        {
            if (_lanePositions.ContainsKey(fret))
            {
                return _lanePositions[fret];
            }

            return (LaneCount - 1) / 2;
        }

        public bool UsingOpenLane { get; private set; }

        private static FiveFretGuitarFret GetFretIndex(FiveLaneKeysAction action)
        {
            return action switch
            {
                FiveLaneKeysAction.OpenNote => FiveFretGuitarFret.Open,
                FiveLaneKeysAction.GreenKey => FiveFretGuitarFret.Green,
                FiveLaneKeysAction.RedKey => FiveFretGuitarFret.Red,
                FiveLaneKeysAction.YellowKey => FiveFretGuitarFret.Yellow,
                FiveLaneKeysAction.BlueKey => FiveFretGuitarFret.Blue,
                FiveLaneKeysAction.OrangeKey => FiveFretGuitarFret.Orange,
                _ => throw new ArgumentOutOfRangeException(nameof(action))
            };
        }

        public int GetLanePosition(FiveFretGuitarFret fret)
        {
            return _lanePositions[(int) fret];
        }

        private static Dictionary<int, int> OPEN_LANE_HIGHWAY_ORDERING = new()
        {
            { (int) FiveFretGuitarFret.Open,    0 },
            { (int) FiveFretGuitarFret.Green,   1 },
            { (int) FiveFretGuitarFret.Red,     2 },
            { (int) FiveFretGuitarFret.Yellow,  3 },
            { (int) FiveFretGuitarFret.Blue,    4 },
            { (int) FiveFretGuitarFret.Orange,  5 }
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

        public KeysEngineParameters EngineParams { get; private set; }

        private FiveFretRangeShift[] _allRangeShiftEvents;
        private readonly Queue<FiveFretRangeShift> _rangeShiftEventQueue = new();
        private FiveFretRangeShift CurrentRange { get; set; }
        private readonly Queue<FiveFretGuitarPlayer.RangeShiftIndicator> _shiftIndicators = new();
        private int _shiftIndicatorIndex;
        private bool _fretPulseStarting;
        private double _fretPulseStartTime;

        private List<int> _activeFrets;

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

            base.Initialize(index, player, chart, trackView, mixer, currentHighScore);
        }

        protected override InstrumentDifficulty<GuitarNote> GetNotes(SongChart chart)
        {
            var track = chart.GetFiveFretTrack(Player.Profile.CurrentInstrument).Clone();
            return track.GetDifficulty(Player.Profile.CurrentDifficulty);
        }

        protected override FiveLaneKeysEngine CreateEngine()
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
                EngineParams = Player.EnginePreset.ProKeys.Create(StarMultiplierThresholds, isBass);
                //EngineParams = EnginePreset.Precision.FiveFretGuitar.Create(StarMultiplierThresholds, isBass);
            }
            else
            {
                // Otherwise, get from the replay
                EngineParams = (KeysEngineParameters) Player.EngineParameterOverride;
            }

            if (EngineContainer != null)
            {
                GameManager.EngineManager.Unregister(EngineContainer);
                EngineContainer = null;
            }

            var engine = new YargFiveLaneKeysEngine(NoteTrack, SyncTrack, EngineParams, Player.Profile.IsBot);
            EngineContainer = GameManager.EngineManager.Register(engine, NoteTrack.Instrument, Chart, Player.RockMeterPreset);

            HitWindow = EngineParams.HitWindow;

            YargLogger.LogFormatDebug("Note count: {0}", NoteTrack.Notes.Count);

            engine.OnNoteHit += OnNoteHit;
            engine.OnNoteMissed += OnNoteMissed;
            engine.OnOverhit += OnOverhit;

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

            StarScoreThresholds = PopulateStarScoreThresholds(StarMultiplierThresholds, Engine.BaseScore);

            IndicatorStripes.Initialize(Player.EnginePreset.FiveFretGuitar);

            MakeHighwayOrdering();

            _fretArray.Initialize(
                _lanePositions,
                LaneCount,
                null,
                Player.ColorProfile.FiveFretGuitar,
                Player.ThemePreset,
                VisualStyle.FiveFretGuitar
            );

            if (Player.Profile.RangeEnabled)
            {
                _activeFrets = new(LaneCount);
                _allRangeShiftEvents = FiveFretRangeShift.GetRangeShiftEvents(NoteTrack);
                InitializeRangeShift();
            }

            BRELanes = new LaneElement[LaneCount];

            LaneElement.DefineLaneScale(Player.Profile.CurrentInstrument, LaneCount);

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

                ((FiveLaneKeysShiftIndicatorElement) poolable).RangeShiftIndicator = shiftIndicator;
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
                if (UsingOpenLane && nextShift.Position is (int)FiveFretGuitarFret.Green)
                {
                    _fretArray.SetFretColorPulse((int) FiveFretGuitarFret.Open, true, (float) nextShift.BeatDuration);
                }

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
            for (var action = FiveLaneKeysAction.GreenKey; action <= FiveLaneKeysAction.OrangeKey; action++)
            {
                _fretArray.SetPressed((int)GetFretIndex(action), Engine.IsKeyHeld(action));
            }

            if (UsingOpenLane)
            {
                _fretArray.SetPressed((int)GetFretIndex(FiveLaneKeysAction.OpenNote), Engine.IsKeyHeld(FiveLaneKeysAction.OpenNote));
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

            ((FiveLaneKeysRangeIndicatorElement) poolable).RangeShift = nextShift;
            poolable.EnableFromPool();

            _shiftIndicators.Dequeue();
        }

        protected override bool InterceptInput(ref GameInput input)
        {
            var action = input.GetAction<ProKeysAction>();

            // Ignore SP in practice mode
            if (action == ProKeysAction.StarPower && GameManager.IsPractice) return true;

            return false;
        }

        protected override void OnInputQueued(GameInput input)
        {
            base.OnInputQueued(input);

            // Update the whammy factor
            if (_sustainCount > 0 && input.GetAction<ProKeysAction>() == ProKeysAction.TouchEffects)
            {
                WhammyFactor = Mathf.Clamp01(input.Axis);
                GameManager.ChangeStemWhammyPitch(_stem, WhammyFactor);
            }
        }

        protected override void InitializeSpawnedNote(IPoolable poolable, GuitarNote note)
        {
            ((FiveLaneKeysNoteElement) poolable).NoteRef = note;
        }

        protected override void InitializeSpawnedLane(LaneElement lane, GuitarNote note)
        {
            lane.SetAppearance(
                Player.Profile.CurrentInstrument,
                note.LaneNote,
                GetLanePositionOrCentered(note.Fret),
                LaneCount,
                Player.ColorProfile.FiveFretGuitar.GetNoteColor(note.Fret).ToUnityColor()
            );
        }

        protected override void InitializeSpawnedLane(LaneElement lane, int laneIndex)
        {
            if (UsingOpenLane)
            {
                if (laneIndex == 0)
                {
                    laneIndex = 7;
                }
            }
            else
            {
                laneIndex++;
            }

            lane.SetAppearance(Player.Profile.CurrentInstrument,
                laneIndex,
                GetLanePositionOrCentered(laneIndex),
                LaneCount,
                Player.ColorProfile.FiveFretGuitar.GetNoteColor(laneIndex).ToUnityColor());
        }

        protected override void ModifyLaneFromNote(LaneElement lane, GuitarNote note)
        {
            if (note.Fret == (int) FiveFretGuitarFret.Open && !UsingOpenLane)
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
            LaneElement.DefineLaneScale(Player.Profile.CurrentInstrument, LaneCount, true);
        }

        private void OnLaneHit(int fret)
        {
            // Adjustment for open lane
            if (fret == 5)
            {
                fret++;
            }

            var index = GetFretIndex((FiveLaneKeysAction)fret).Convert();
            _fretArray.PlayCodaHitAnimation(index);
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


        protected override void OnNoteHit(int index, GuitarNote note)
        {
            base.OnNoteHit(index, note);

            if (GameManager.Paused) return;

            (NotePool.GetByKey(note) as FiveLaneKeysNoteElement)?.HitNote();

            if (note.FiveLaneKeysAction is FiveLaneKeysAction.OpenNote && !UsingOpenLane)
            {
                _fretArray.PlayOpenHitAnimation();
            }
            else
            {
                _fretArray.PlayHitAnimation((int)GetFretIndex(note.FiveLaneKeysAction));
            }
        }

        protected override void OnNoteMissed(int index, GuitarNote chordParent)
        {
            base.OnNoteMissed(index, chordParent);

            (NotePool.GetByKey(chordParent) as FiveLaneKeysNoteElement).MissNote();
        }

        private void OnOverhit(int key)
        {
            OnOverhit();

            if (key is (int) FiveLaneKeysAction.OpenNote && !UsingOpenLane)
            {
                _fretArray.PlayOpenMissAnimation();
            }
            else
            {
                _fretArray.PlayMissAnimation((int)GetFretIndex((FiveLaneKeysAction)key));
            }
        }

        private void OnSustainStart(GuitarNote note)
        {
            if (note.FiveLaneKeysAction is not FiveLaneKeysAction.OpenNote || UsingOpenLane)
            {
                _fretArray.SetSustained((int)GetFretIndex(note.FiveLaneKeysAction), true);
            }

            _sustainCount++;
        }

        private void OnSustainEnd(GuitarNote note, double timeEnded, bool finished)
        {
            (NotePool.GetByKey(note) as FiveLaneKeysNoteElement)?.SustainEnd(finished);

            // Mute the stem if you let go of the sustain too early.
            // Leniency is handled by the engine's sustain burst threshold.
            if (!finished)
            {
                // Do we want to check if its part of a chord, and if so, if all sustains were dropped to mute?
                SetStemMuteState(true);
            }

            if (note.FiveLaneKeysAction is not FiveLaneKeysAction.OpenNote || UsingOpenLane)
            {
                _fretArray.SetSustained((int) GetFretIndex(note.FiveLaneKeysAction), false);
            }

            _sustainCount--;

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
                (note as FiveLaneKeysNoteElement)?.OnStarPowerUpdated();
            }
        }

        public override (ReplayFrame Frame, ReplayStats Stats) ConstructReplayData()
        {
            var frame = new ReplayFrame(Player.Profile, EngineParams, Engine.EngineStats, ReplayInputs.ToArray());
            return (frame, Engine.EngineStats.ConstructReplayStats(Player.Profile.Name));
        }

        public override void SetStemMuteState(bool muted)
        {
            if (IsStemMuted != muted)
            {
                GameManager.ChangeStemMuteState(_stem, muted);
                IsStemMuted = muted;
            }
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

                var shiftRight = shift.Position > lastShiftRange.Position;

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

                    int offset;

                    var openLaneAdjustment = UsingOpenLane ? 0 : 1;

                    if (shiftRight)
                    {
                        offset = LaneCount - shift.Position - shift.Size + openLaneAdjustment;
                    }
                    else
                    {
                        if (UsingOpenLane && shift.Position is (int)FiveFretGuitarFret.Green)
                        {
                            offset = 0; // When shifting down to GRY[B] in open lane mode, treat P as part of the range
                        }
                        else
                        {
                            offset = shift.Position - openLaneAdjustment;
                        }
                    }


                    _shiftIndicators.Enqueue(new FiveFretGuitarPlayer.RangeShiftIndicator
                    {
                        Time = beatlines[realIndex].Time,
                        RightSide = shiftRight,
                        Offset = offset,
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

            // When using the open lane, assume opens are fair game in GRY[B] ranges and not higher ones.
            // This isn't strictly true, and we can consider an explicit text event to control this, but
            // we definitely don't want to test at runtime for whether there's an open between now and the
            // next shift, so this will have to do for now.
            if (UsingOpenLane && start is (int)FiveFretGuitarFret.Green)
            {
                newFrets.Add((int)FiveFretGuitarFret.Open);
            }

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
            UsingOpenLane = ShouldUseOpenLane();

            if (UsingOpenLane)
            {
                LaneCount = 6;
                _lanePositions = OPEN_LANE_HIGHWAY_ORDERING;
            }
            else
            {
                LaneCount = 5;
                _lanePositions = FiveFretGuitarPlayer.DEFAULT_HIGHWAY_ORDERING;
            }
        }

        private bool ShouldUseOpenLane()
        {
            switch (Player.Profile.OpenLaneDisplayType)
            {
                case OpenLaneDisplayType.Never:
                    return false;
                case OpenLaneDisplayType.Always:
                    return true;
                case OpenLaneDisplayType.IfChartContainsOpens:
                    foreach (var note in NoteTrack.Notes)
                    {
                        foreach (var child in note.AllNotes)
                        {
                            if (child.Fret is (int)FiveFretGuitarFret.Open)
                            {
                                return true;
                            }
                        }
                    }
                    return false;
                default:
                    throw new ArgumentOutOfRangeException("Unrecognized OpenLaneDisplayType");
            }
        }

        protected override Dictionary<int, int> GetLaneIndexes()
        {
            if (UsingOpenLane)
            {
                return new Dictionary<int, int>
                {
                    { (int) FiveLaneKeysAction.GreenKey, 1 },
                    { (int) FiveLaneKeysAction.RedKey, 2 },
                    { (int) FiveLaneKeysAction.YellowKey, 3 },
                    { (int) FiveLaneKeysAction.BlueKey, 4 },
                    { (int) FiveLaneKeysAction.OrangeKey, 5 },
                    { (int) FiveLaneKeysAction.OpenNote, 0 }
                };
            }

            return new Dictionary<int, int>
            {
                { (int) FiveLaneKeysAction.GreenKey, 0 },
                { (int) FiveLaneKeysAction.RedKey, 1 },
                { (int) FiveLaneKeysAction.YellowKey, 2 },
                { (int) FiveLaneKeysAction.BlueKey, 3 },
                { (int) FiveLaneKeysAction.OrangeKey, 4 }
            };
        }
    }
}
