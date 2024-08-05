using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using YARG.Core;
using YARG.Core.Audio;
using YARG.Core.Chart;
using YARG.Core.Engine.ProKeys;
using YARG.Core.Engine.ProKeys.Engines;
using YARG.Core.Input;
using YARG.Core.Logging;
using YARG.Gameplay.Visuals;

namespace YARG.Gameplay.Player
{
    public class ProKeysPlayer : TrackPlayer<ProKeysEngine, ProKeysNote>
    {
        public struct RangeShift
        {
            public double Time;
            public double TimeLength;

            public uint Tick;
            public uint TickLength;

            public int Key;
        }

        public struct RangeShiftIndicator
        {
            public double Time;
            public bool LeftSide;
        }

        public const int WHITE_KEY_VISIBLE_COUNT = 10;
        public const int TOTAL_KEY_COUNT = 25;

        private const int SHIFT_INDICATOR_MEASURES_BEFORE = 4;

        public override float[] StarMultiplierThresholds { get; protected set; } =
        {
            0.21f, 0.46f, 0.77f, 1.85f, 3.08f, 4.52f
        };

        public override int[] StarScoreThresholds { get; protected set; }

        public ProKeysEngineParameters EngineParams { get; private set; }

        public override bool ShouldUpdateInputsOnResume => true;

        public float RangeShiftOffset => _currentOffset;

        [Header("Pro Keys Specific")]
        [SerializeField]
        private KeysArray _keysArray;
        [SerializeField]
        private ProKeysTrackOverlay _trackOverlay;
        [SerializeField]
        private Pool _shiftIndicatorPool;
        [SerializeField]
        private KeyedPool _chordBarPool;

        private List<RangeShift> _rangeShifts;
        private readonly List<RangeShiftIndicator> _shiftIndicators = new();

        private int _rangeShiftIndex;
        private int _shiftIndicatorIndex;

        private bool _isOffsetChanging;

        private double _offsetStartTime;
        private double _offsetEndTime;

        private float _previousOffset;
        private float _currentOffset;
        private float _targetOffset;

        protected override InstrumentDifficulty<ProKeysNote> GetNotes(SongChart chart)
        {
            var track = chart.ProKeys.Clone();
            return track.GetDifficulty(Player.Profile.CurrentDifficulty);
        }

        protected override ProKeysEngine CreateEngine()
        {
            if (!GameManager.IsReplay)
            {
                // Create the engine params from the engine preset
                EngineParams = Player.EnginePreset.ProKeys.Create(StarMultiplierThresholds);
            }
            else
            {
                // Otherwise, get from the replay
                EngineParams = (ProKeysEngineParameters) Player.EngineParameterOverride;
            }

            var engine = new YargProKeysEngine(NoteTrack, SyncTrack, EngineParams, Player.Profile.IsBot);

            HitWindow = EngineParams.HitWindow;

            YargLogger.LogFormatDebug("Note count: {0}", NoteTrack.Notes.Count);

            engine.OnNoteHit += OnNoteHit;
            engine.OnNoteMissed += OnNoteMissed;
            engine.OnOverhit += OnOverhit;

            engine.OnSustainStart += OnSustainStart;
            engine.OnSustainEnd += OnSustainEnd;

            engine.OnSoloStart += OnSoloStart;
            engine.OnSoloEnd += OnSoloEnd;

            engine.OnStarPowerPhraseHit += OnStarPowerPhraseHit;
            engine.OnStarPowerStatus += OnStarPowerStatus;

            engine.OnKeyStateChange += OnKeyStateChange;

            engine.OnCountdownChange += OnCountdownChange;

            return engine;
        }

        protected override void FinishInitialization()
        {
            base.FinishInitialization();

            GetRangeShifts();

            _keysArray.Initialize(this, Player.ThemePreset, Player.ColorProfile.ProKeys);
            _trackOverlay.Initialize(this, Player.ColorProfile.ProKeys);

            if (_rangeShifts.Count > 0)
            {
                RangeShiftTo(_rangeShifts[0].Key, 0);
                _rangeShiftIndex++;
            }
        }

        public override void ResetPracticeSection()
        {
            base.ResetPracticeSection();

            _rangeShiftIndex = 0;
            _shiftIndicatorIndex = 0;

            if (_rangeShifts.Count > 0)
            {
                RangeShiftTo(_rangeShifts[0].Key, 0);
                _rangeShiftIndex++;
            }
        }

        public override void SetPracticeSection(uint start, uint end)
        {
            base.SetPracticeSection(start, end);

            GetRangeShifts();

            // This should never happen unless the chart has no range shifts, which is just bad charting
            if (_rangeShifts.Count == 0)
            {
                YargLogger.LogWarning("No range shifts found in chart. Defaulting to 0.");
                RangeShiftTo(0, 0);
                _rangeShiftIndex++;

                return;
            }

            _rangeShiftIndex = 0;
            _shiftIndicatorIndex = 0;

            int startIndex = _rangeShifts.FindIndex(r => r.Tick >= start);

            // No range shifts were >= start, so get the one prior.
            if(startIndex == -1)
            {
                startIndex = _rangeShifts.FindLastIndex(r => r.Tick < start);
            }

            // If the range shift is not on the starting tick, get the one before it.
            // This is so that the correct range is used at the start of the section.
            if (_rangeShifts[startIndex].Tick > start && startIndex > 0)
            {
                // Only get the previous range shift if there are notes before the current first range shift.
                // If there are no notes, we can just automatically shift to it at the start of the section like in Quickplay.
                if (Notes.Count > 0 && Notes[0].Tick < _rangeShifts[startIndex].Tick)
                {
                    startIndex--;
                }
            }

            int endIndex = _rangeShifts.FindIndex(r => r.Tick >= end);
            if (endIndex == -1)
            {
                endIndex = _rangeShifts.Count;
            }

            _rangeShifts = _rangeShifts.GetRange(startIndex, endIndex - startIndex);

            if (_rangeShifts.Count > 0)
            {
                RangeShiftTo(_rangeShifts[0].Key, 0);
                _rangeShiftIndex++;
            }
        }

        protected override void OnNoteHit(int index, ProKeysNote note)
        {
            base.OnNoteHit(index, note);

            if (GameManager.Paused) return;

            (NotePool.GetByKey(note) as ProKeysNoteElement)?.HitNote();
            _keysArray.PlayHitAnimation(note.Key);

            // Chord bars are spawned based on the parent element
            var parent = note.ParentOrSelf;
            (_chordBarPool.GetByKey(parent) as ProKeysChordBarElement)?.CheckForChordHit();
        }

        protected override void OnNoteMissed(int index, ProKeysNote chordParent)
        {
            base.OnNoteMissed(index, chordParent);

            (NotePool.GetByKey(chordParent) as ProKeysNoteElement)?.MissNote();
        }

        private void OnOverhit(int key)
        {
            OnOverhit();

            // do overhit visuals
        }

        private void OnSustainStart(ProKeysNote parent)
        {

        }

        private void OnSustainEnd(ProKeysNote parent, double timeEnded, bool finished)
        {
            (NotePool.GetByKey(parent) as ProKeysNoteElement)?.SustainEnd(finished);

            // Mute the stem if you let go of the sustain too early.
            // Leniency is handled by the engine's sustain burst threshold.
            if (!finished)
            {
                // Do we want to check if its part of a chord, and if so, if all sustains were dropped to mute?
                SetStemMuteState(true);
            }
        }

        private void OnKeyStateChange(int key, bool isPressed)
        {
            _trackOverlay.SetKeyHeld(key, isPressed);
            _keysArray.SetPressed(key, isPressed);
        }

        private void RangeShiftTo(int noteIndex, double timeLength)
        {
            _isOffsetChanging = true;

            _offsetStartTime = GameManager.RealVisualTime;
            _offsetEndTime = GameManager.RealVisualTime + timeLength;

            _previousOffset = _currentOffset;

            // We need to get the offset relative to the 0th key (as that's the base)
            _targetOffset = _keysArray.GetKeyX(0) - _keysArray.GetKeyX(noteIndex);
        }

        public float GetNoteX(int index)
        {
            return _keysArray.GetKeyX(index) + _currentOffset;
        }

        protected override void UpdateVisuals(double songTime)
        {
            UpdateBaseVisuals(Engine.EngineStats, EngineParams, songTime);
            UpdatePhrases(songTime);

            if (_isOffsetChanging)
            {
                float changePercent = (float) YargMath.InverseLerpD(_offsetStartTime, _offsetEndTime,
                    GameManager.RealVisualTime);

                // Because the range shift is called when resetting practice mode, the start time
                // will be that of the previous section causing the real time to be less than the start time.
                // In that case, just complete the range shift immediately.
                if (GameManager.RealVisualTime < _offsetStartTime)
                {
                    changePercent = 1f;
                }

                if (changePercent >= 1f)
                {
                    // If the change has finished, stop!
                    _isOffsetChanging = false;
                    _currentOffset = _targetOffset;
                }
                else
                {
                    _currentOffset = Mathf.Lerp(_previousOffset, _targetOffset, changePercent);
                }

                // Update the visuals with the new offsets

                var keysTransform = _keysArray.transform;
                keysTransform.localPosition = keysTransform.localPosition.WithX(_currentOffset);

                var overlayTransform = _trackOverlay.transform;
                overlayTransform.localPosition = overlayTransform.localPosition.WithX(_currentOffset);

                foreach (var note in NotePool.AllSpawned)
                {
                    (note as ProKeysNoteElement)?.UpdateXPosition();
                }

                foreach (var bar in _chordBarPool.AllSpawned)
                {
                    (bar as ProKeysChordBarElement)?.UpdateXPosition();
                }
            }
        }

        private void UpdatePhrases(double songTime)
        {
            while (_rangeShiftIndex < _rangeShifts.Count && _rangeShifts[_rangeShiftIndex].Time <= songTime)
            {
                var rangeShift = _rangeShifts[_rangeShiftIndex];

                const double rangeShiftTime = 0.25;
                RangeShiftTo(rangeShift.Key, rangeShiftTime);

                _rangeShiftIndex++;
            }

            while (_shiftIndicatorIndex < _shiftIndicators.Count
                && _shiftIndicators[_shiftIndicatorIndex].Time <= songTime + SpawnTimeOffset)
            {
                var shiftIndicator = _shiftIndicators[_shiftIndicatorIndex];

                // Skip this frame if the pool is full
                if (!_shiftIndicatorPool.CanSpawnAmount(1))
                {
                    break;
                }

                var poolable = _shiftIndicatorPool.TakeWithoutEnabling();
                if (poolable == null)
                {
                    YargLogger.LogWarning("Attempted to spawn shift indicator, but it's at its cap!");
                    break;
                }

                YargLogger.LogDebug("Shift indicator spawned!");

                ((ProKeysShiftIndicatorElement) poolable).RangeShiftIndicator = shiftIndicator;
                poolable.EnableFromPool();

                _shiftIndicatorIndex++;
            }
        }

        public override void SetStemMuteState(bool muted)
        {
            if (IsStemMuted != muted)
            {
                GameManager.ChangeStemMuteState(SongStem.Keys, muted);
                IsStemMuted = muted;
            }
        }

        protected override void InitializeSpawnedNote(IPoolable poolable, ProKeysNote note)
        {
            ((ProKeysNoteElement) poolable).NoteRef = note;
        }

        protected override void OnNoteSpawned(ProKeysNote parentNote)
        {
            if (parentNote.WasHit || parentNote.ChildNotes.Count <= 0)
            {
                return;
            }

            if (!_chordBarPool.CanSpawnAmount(1))
            {
                return;
            }

            var poolable = _chordBarPool.KeyedTakeWithoutEnabling(parentNote);
            if (poolable == null)
            {
                YargLogger.LogWarning("Attempted to spawn shift indicator, but it's at its cap!");
                return;
            }

            ((ProKeysChordBarElement) poolable).NoteRef = parentNote;
            poolable.EnableFromPool();
        }

        protected override bool InterceptInput(ref GameInput input)
        {
            var action = input.GetAction<ProKeysAction>();

            // Ignore SP in practice mode
            if (action == ProKeysAction.StarPower && GameManager.IsPractice) return true;

            return false;
        }

        private void GetRangeShifts()
        {
            // Get the range shifts from the phrases

            _rangeShifts = NoteTrack.Phrases
                .Where(phrase => phrase.Type is >= PhraseType.ProKeys_RangeShift0 and <= PhraseType.ProKeys_RangeShift5)
                .Select(phrase =>
                {
                    return new RangeShift
                    {
                        Time = phrase.Time,
                        TimeLength = phrase.TimeLength,

                        Tick = phrase.Tick,
                        TickLength = phrase.TickLength,

                        Key = phrase.Type switch
                        {
                            PhraseType.ProKeys_RangeShift0 => 0,
                            PhraseType.ProKeys_RangeShift1 => 2,
                            PhraseType.ProKeys_RangeShift2 => 4,
                            PhraseType.ProKeys_RangeShift3 => 5,
                            PhraseType.ProKeys_RangeShift4 => 7,
                            PhraseType.ProKeys_RangeShift5 => 9,
                            _                              => throw new Exception("Unreachable")
                        }
                    };
                })
                .ToList();

            // Get the range shift change indicator times based on the strong beatlines

            var beatlines = Beatlines
                .Where(i => i.Type is BeatlineType.Measure or BeatlineType.Strong)
                .ToList();

            _shiftIndicators.Clear();
            int lastShiftKey = 0;
            int beatlineIndex = 0;

            foreach (var shift in _rangeShifts)
            {
                if (shift.Key == lastShiftKey)
                {
                    continue;
                }

                var shiftLeft = shift.Key > lastShiftKey;
                lastShiftKey = shift.Key;

                // Look for the closest beatline index. Since the range shifts are
                // in order, we can just continuously look for the correct beatline
                for (; beatlineIndex < beatlines.Count; beatlineIndex++)
                {
                    if (beatlines[beatlineIndex].Time > shift.Time)
                    {
                        break;
                    }
                }

                // Add the indicators before the range shift
                for (int i = SHIFT_INDICATOR_MEASURES_BEFORE; i >= 1; i--)
                {
                    var realIndex = beatlineIndex - i;

                    // If the indicator is before any measures, skip
                    if (realIndex < 0)
                    {
                        break;
                    }

                    _shiftIndicators.Add(new RangeShiftIndicator
                    {
                        Time = beatlines[realIndex].Time,
                        LeftSide = shiftLeft
                    });
                }
            }
        }
    }
}