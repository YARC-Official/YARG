using System.Collections.Generic;
using UnityEngine;
using YARG.Core;
using YARG.Core.Chart;
using YARG.Core.Engine;

namespace YARG.Venue
{
    public sealed class VocalChannel : IVenueChannel
    {
        private readonly Animator         _animator;
        private readonly VenueHashLibrary _hashes;
        private readonly int              _leadingFrames;
        private readonly int              _instrumentIndex; // 0=Vocal, 1=Har1, 2=Har2

        private EngineManager _engineManager;
        private List<VocalNote> _notes = new();
        private int _currentNoteIndex;
        private int _prevPitchIndex = -1;

        public VocalChannel(Animator animator, VenueHashLibrary hashes, int leadingFrames, int instrumentIndex)
        {
            _animator = animator;
            _hashes = hashes;
            _leadingFrames = leadingFrames;
            _instrumentIndex = instrumentIndex;
        }

        public void BuildCommands(SongChart chart, AnimatorCommandQueue queue)
        {
            _notes.Clear();
            InstrumentDifficulty<VocalNote> instrument = null;
            if (_instrumentIndex == 0)
            {
                if (chart.Harmony.Parts.Count > 0)
                    instrument = chart.Harmony.Parts[0].CloneAsInstrumentDifficulty();

                if (instrument == null || instrument.Notes.Count == 0)
                {
                    if (chart.Vocals.Parts.Count > 0)
                        instrument = chart.Vocals.Parts[0].CloneAsInstrumentDifficulty();
                }
            }
            else if (chart.Harmony.Parts.Count > _instrumentIndex)
            {
                instrument = chart.Harmony.Parts[_instrumentIndex].CloneAsInstrumentDifficulty();
            }

            if (instrument == null) return;

            foreach (var note in instrument.Notes)
            {
                var phraseClone = note.Clone();
                foreach (var phraseNote in phraseClone.ChildNotes)
                {
                    _notes.Add(phraseNote);
                    double t = phraseNote.Time - _leadingFrames / 60.0;

                    int noteOnHash = _instrumentIndex == 0 ? _hashes.VocalNote :
                                    _instrumentIndex == 1 ? _hashes.Har1Note : _hashes.Har2Note;

                    queue.Add(AnimatorCommand.Randomize(t, _animator));
                    queue.Add(AnimatorCommand.BoolOn(t, _animator, noteOnHash, (float) phraseNote.TotalTimeLength));

                    if (phraseNote.IsNonPitched)
                    {
                        int unpitchedHash = _instrumentIndex == 0 ? _hashes.VocalUnpitched :
                                           _instrumentIndex == 1 ? _hashes.Har1Unpitched : _hashes.Har2Unpitched;
                        queue.Add(AnimatorCommand.Trigger(t, _animator, unpitchedHash));
                    }
                }
            }
        }

        public void Update(double visualTime)
        {
            while (_currentNoteIndex < _notes.Count && _notes[_currentNoteIndex].TotalTimeEnd < visualTime)
            {
                _currentNoteIndex++;
                _prevPitchIndex = -1;
            }

            if (_currentNoteIndex < _notes.Count && (
                visualTime >= _notes[_currentNoteIndex].Time &&
                visualTime <= _notes[_currentNoteIndex].TotalTimeEnd))
            {
                var note = _notes[_currentNoteIndex];
                float pitch = note.PitchAtSongTime(visualTime);

                int pitchHash = _instrumentIndex switch
                {
                    0 => _hashes.VocalPitch,
                    1 => _hashes.Har1Pitch,
                    _ => _hashes.Har2Pitch
                };

                _animator.SafeSetFloat(pitchHash, pitch);

                int pitchIndex = Mathf.Clamp(Mathf.RoundToInt(pitch), 35, 100) - 35;
                if (pitchIndex != _prevPitchIndex)
                {
                    int noteHash = _hashes.VocalNoteHashes[_instrumentIndex * _hashes.VocalNoteCount + pitchIndex];
                    _animator.SafeSetTrigger(noteHash);
                    _prevPitchIndex = pitchIndex;
                }
            }
        }

        public void Initialize(EngineManager manager)
        {
            _engineManager = manager;
            _currentNoteIndex = 0;
            _prevPitchIndex = -1;
        }
    }
}