using System.Collections.Generic;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Gameplay.Visuals;

namespace YARG.Gameplay.Player
{
    public class VocalPercussionTrack : GameplayBehaviour
    {
        [SerializeField]
        private KeyedPool _pool;

        // Because we want the keyed pool to work properly, we need to keep the note
        // references the same as in the player. We cannot use a VocalsPart in this case.
        private List<VocalNote> _notes;

        private int _phraseIndex;
        private int _noteIndex;

        private VocalNote CurrentNote =>
            _notes[_phraseIndex].ChildNotes[_noteIndex];

        private bool CurrentPhraseInBounds =>
            _phraseIndex < _notes.Count;
        private bool CurrentNoteInBounds =>
            CurrentPhraseInBounds &&
            _noteIndex < _notes[_phraseIndex].ChildNotes.Count;

        public void Initialize(List<VocalNote> notes)
        {
            _notes = notes;
        }

        private void Update()
        {
            while (CurrentNoteInBounds && CurrentNote.Time <= GameManager.SongTime + VocalTrack.SPAWN_TIME_OFFSET)
            {
                var note = CurrentNote;

                if (note.IsPercussion)
                {
                    // Skip this frame if the pool is full
                    if (!_pool.CanSpawnAmount(1))
                    {
                        return;
                    }

                    // Spawn the percussion note
                    var noteObj = _pool.KeyedTakeWithoutEnabling(note);
                    ((VocalPercussionElement) noteObj).NoteRef = note;
                    noteObj.EnableFromPool();
                }

                // Go to the next note (and the next phrase if necessary)
                _noteIndex++;
                if (!CurrentNoteInBounds)
                {
                    // Make sure to skip all of the empty phrases
                    do
                    {
                        _phraseIndex++;
                        _noteIndex = 0;
                    } while (CurrentPhraseInBounds && !CurrentNoteInBounds);
                }
            }
        }

        public void HitPercussionNote(VocalNote note)
        {
            var obj = _pool.GetByKey(note);
            _pool.Return(obj);
        }
    }
}