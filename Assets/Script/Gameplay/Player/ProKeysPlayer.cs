using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using YARG.Core;
using YARG.Core.Audio;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Core.Engine.ProKeys;
using YARG.Core.Engine.ProKeys.Engines;
using YARG.Core.Input;
using YARG.Core.Logging;
using YARG.Gameplay.Visuals;
using YARG.Helpers;

namespace YARG.Gameplay.Player
{
    public class ProKeysPlayer : TrackPlayer<ProKeysEngine, ProKeysNote>
    {
        public const int WHITE_KEY_VISIBLE_COUNT = 10;
        public const int TOTAL_KEY_COUNT = 25;

        public override float[] StarMultiplierThresholds { get; protected set; } =
        {
            0.21f, 0.46f, 0.77f, 1.85f, 3.08f, 4.52f
        };

        public override int[] StarScoreThresholds { get; protected set; }

        public ProKeysEngineParameters EngineParams { get; private set; }

        public override bool ShouldUpdateInputsOnResume => true;

        [Header("Pro Keys Specific")]
        [SerializeField]
        private KeysArray _keysArray;
        [SerializeField]
        private ProKeysTrackOverlay _trackOverlay;

        private List<ProKeysRangeShift> _rangeShifts;

        private int _phraseIndex;
        private int _rangeShiftIndex;

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
                // EngineParams = Player.EnginePreset.FiveFretGuitar.Create(StarMultiplierThresholds, isBass);
                EngineParams = new ProKeysEngineParameters(new HitWindowSettings(0.14, 0.14, 1.0, false), 4,
                    StarMultiplierThresholds);
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

            engine.OnSoloStart += OnSoloStart;
            engine.OnSoloEnd += OnSoloEnd;

            engine.OnStarPowerPhraseHit += OnStarPowerPhraseHit;
            engine.OnStarPowerStatus += OnStarPowerStatus;

            return engine;
        }

        protected override void FinishInitialization()
        {
            base.FinishInitialization();

            _rangeShifts = NoteTrack.Phrases.Where(phrase =>
                phrase.Type is >= PhraseType.ProKeys_RangeShift0 and <= PhraseType.ProKeys_RangeShift5).Select(phrase =>
            {
                return new ProKeysRangeShift { Time = phrase.Time, TimeLength = phrase.TimeLength, Key = phrase.Type switch
                {
                    PhraseType.ProKeys_RangeShift0 => 0,
                    PhraseType.ProKeys_RangeShift1 => 2,
                    PhraseType.ProKeys_RangeShift2 => 4,
                    PhraseType.ProKeys_RangeShift3 => 5,
                    PhraseType.ProKeys_RangeShift4 => 7,
                    PhraseType.ProKeys_RangeShift5 => 9,
                    _ => throw new Exception("Unreachable")
                } };
            }).ToList();

            _keysArray.Initialize(Player.ThemePreset, Player.ColorProfile.ProKeys);
            _trackOverlay.Initialize(this, Player.ColorProfile.ProKeys);

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
                    (note as ProKeysNoteElement)?.UpdateNoteX();
                }
            }
        }

        private void UpdatePhrases(double songTime)
        {
            var phrases = NoteTrack.Phrases;

            while (_phraseIndex < phrases.Count && phrases[_phraseIndex].Time <= songTime)
            {
                var phrase = phrases[_phraseIndex];
                _phraseIndex++;
            }

            while (_rangeShiftIndex < _rangeShifts.Count && _rangeShifts[_rangeShiftIndex].Time <= songTime)
            {
                var rangeShift = _rangeShifts[_rangeShiftIndex];
                _rangeShiftIndex++;

                RangeShiftTo(rangeShift.Key, rangeShift.TimeLength);
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

        protected override bool InterceptInput(ref GameInput input)
        {
            var action = input.GetAction<ProKeysAction>();
            if (action != ProKeysAction.StarPower && action != ProKeysAction.TouchEffects)
            {
                int key = (int) action;
                _trackOverlay.SetKeyHeld(key, input.Button);
            }

            // Ignore SP in practice mode
            if (action == ProKeysAction.StarPower && GameManager.IsPractice) return true;

            return false;
        }
    }

    public struct ProKeysRangeShift
    {
        public double Time;
        public double TimeLength;

        public int Key;
    }
}