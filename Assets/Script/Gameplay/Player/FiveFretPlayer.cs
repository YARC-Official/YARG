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

namespace YARG.Gameplay.Player
{
    public sealed class FiveFretPlayer : TrackPlayer<GuitarEngine, GuitarNote>
    {
        private const double SUSTAIN_END_MUTE_THRESHOLD      = 0.1;
        private const int    SHIFT_INDICATOR_MEASURES_BEFORE = 5;

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
        }

        private Queue<FiveFretRangeShift>  _rangeShiftEvents;
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

        public override float[] StarMultiplierThresholds { get; protected set; } =
            GuitarStarMultiplierThresholds;

        public override int[] StarScoreThresholds { get; protected set; }

        public float WhammyFactor { get; private set; }

        private int _sustainCount;

        private SongStem _stem;

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

            _fretArray.ResetAll();
        }

        // TODO: Figure out why this isn't a valid declaration so we can fix replay seeking
        // public override void SetReplayTime(float time)
        // {
        //     base.SetReplayTime(time);
        // }

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
            if (!_rangeShiftEvents.TryPeek(out var nextShift))
            {
                return;
            }

            if (!nextShift.Shown && nextShift.Time >= songTime - SpawnTimeOffset)
            {
                var shiftLeft = nextShift.Range > CurrentRange.Range;
                nextShift.Shown = true;
            }

            if (_fretPulseStarting && _fretPulseStartTime <= songTime)
            {
                for (var i = nextShift.Range - 1; i < nextShift.Range + nextShift.Size - 1; i++)
                {
                    _fretArray.SetFretColorPulse(i, true, (float) nextShift.BeatDuration);
                }

                _fretPulseStarting = false;
            }

            if (_shiftIndicators.TryPeek(out var shiftIndicator) && shiftIndicator.Time <= songTime + SpawnTimeOffset)
            {
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

                // TODO: Adjust this to start when the first shift indicator reaches the strike line
                if (!_fretPulseStarting)
                {
                    _fretPulseStarting = true;
                    // Why is this not producing the right time?
                    // It is supposed to start pulsing when the shift indicator hits the strike line
                    // Time - -STRIKE_LINE_POS / NoteSpeed is too early by something like 4 beats
                    // Time alone is too late by a couple of beats

                    // This works, but the math is all wrong and just happens to produce the right answer
                    _fretPulseStartTime = shiftIndicator.Time - ((-STRIKE_LINE_POS / NoteSpeed) / 2) + nextShift.BeatDuration * 1.25;
                }
            }

            // TODO: Also need to deal with seeking in replays somewhere
            if (nextShift.Time <= songTime)
            {
                _rangeShiftEvents.Dequeue();
                for (var i = 0; i < _fretArray.FretCount; i++)
                {
                    _fretArray.SetFretColorPulse(i, false, (float) nextShift.BeatDuration);
                }

                CurrentRange = nextShift;
                SetActiveFretsForShiftEvent(nextShift);
            }
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


        private void InitializeRangeShift()
        {
            // Default to everything on
            _activeFrets = new bool[_fretArray.FretCount];
            for (int i = 0; i < _fretArray.FretCount; i++)
            {
                _activeFrets[i] = true;
            }

            var events = FiveFretRangeShift.GetRangeShiftEvents(NoteTrack.TextEvents, NoteTrack.Difficulty);
            // Fewer than two range shifts makes no sense
            if (events.Count < 1)
            {
                _rangeShiftEvents = new Queue<FiveFretRangeShift>();
                return;
            }

            if (events.Count == 1)
            {
                // There are no actual shifts, but we should dim unused frets
                SetActiveFretsForShiftEvent(events[0]);
                CurrentRange = events[0];
                _rangeShiftEvents = new Queue<FiveFretRangeShift>();
                return;
            }

            // Turns out that we have range shifts that need indicators
            var firstEvent = events[0];
            CurrentRange = firstEvent;
            SetActiveFretsForShiftEvent(CurrentRange);
            events.RemoveAt(0);
            _rangeShiftEvents = new Queue<FiveFretRangeShift>(events);

            // Figure out where the indicators should go
            var beatlines = Beatlines
                .Where(i => i.Type is BeatlineType.Measure or BeatlineType.Strong)
                .ToList();

            _shiftIndicators.Clear();
            int lastShiftRange = firstEvent.Range;
            int beatlineIndex = 0;

            foreach (var shift in _rangeShiftEvents.ToList())
            {
                if (shift.Range == lastShiftRange)
                {
                    continue;
                }

                var shiftLeft = shift.Range > lastShiftRange;
                lastShiftRange = shift.Range;

                int beatTimeSamples = 0;
                double beatTimeDelta = 0;
                double lastBeatTime = 0;

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
                for (int i = SHIFT_INDICATOR_MEASURES_BEFORE; i > 1; i--)
                {
                    var realIndex = beatlineIndex - i;

                    // If the indicator is before any measures, skip
                    if (realIndex < 0)
                    {
                        break;
                    }

                    beatTimeDelta += lastBeatTime - beatlines[realIndex].Time;
                    lastBeatTime = beatlines[realIndex].Time;
                    beatTimeSamples++;

                    _shiftIndicators.Enqueue(new RangeShiftIndicator
                    {
                        Time = beatlines[realIndex].Time,
                        LeftSide = shiftLeft
                    });
                }

                // In case we have no samples for this shift event, 0.5 is a reasonable default
                shift.BeatDuration = beatTimeSamples > 0 ? beatTimeDelta / beatTimeSamples : 0.5;
            }

            // TODO: Remove this test shit
            // _fretArray.UpdateFretActiveState(_activeFrets);
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