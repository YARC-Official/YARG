using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Gameplay.Visuals;
using YARG.Helpers.Authoring;

namespace YARG.Gameplay.Player
{
    public class VocalPercussionTrack : GameplayBehaviour
    {
        // Time offset relative to 1.0 note speed
        private const float SPAWN_TIME_OFFSET = 25f;

        [SerializeField]
        private KeyedPool _pool;

        [Space]
        [SerializeField]
        private Transform _percussionFret;
        [SerializeField]
        private EffectGroup _hitEffects;

        // Because we want the keyed pool to work properly, we need to keep the note
        // references the same as in the player. We cannot use a VocalsPart in this case.
        private List<VocalNote> _notes;

        private int _phraseIndex;
        private int _noteIndex;

        // TODO: Consolidate this with VocalTrack's SpawnTimeOffset
        public float TrackSpeed { get; set; }
        private float SpawnTimeOffset => SPAWN_TIME_OFFSET / TrackSpeed;

        private VocalNote CurrentNote =>
            _notes[_phraseIndex].ChildNotes[_noteIndex];

        private bool CurrentPhraseInBounds =>
            _phraseIndex < _notes.Count;
        private bool CurrentNoteInBounds =>
            CurrentPhraseInBounds &&
            _noteIndex < _notes[_phraseIndex].ChildNotes.Count;

        private Coroutine _fretShowCoroutine;

        public void Initialize(List<VocalNote> notes)
        {
            _notes = notes;
        }

        private void Update()
        {
            while (CurrentNoteInBounds && CurrentNote.Time <= GameManager.SongTime + SpawnTimeOffset)
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

            _hitEffects.Play();
        }

        public void ShowPercussionFret(bool show)
        {
            if (_fretShowCoroutine != null)
            {
                StopCoroutine(_fretShowCoroutine);
            }

            if (show == _percussionFret.gameObject.activeSelf)
            {
                return;
            }

            StartCoroutine(ShowPercussionFretAnimation(show));
        }

        private IEnumerator ShowPercussionFretAnimation(bool show)
        {
            if (show)
            {
                _percussionFret.gameObject.SetActive(true);

                _percussionFret.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
                yield return _percussionFret
                    .DORotate(new Vector3(90f, 0f, 0f), 0.25f)
                    .WaitForCompletion();
            }
            else
            {
                _percussionFret.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                yield return _percussionFret
                    .DORotate(new Vector3(0f, 0f, 0f), 0.25f)
                    .WaitForCompletion();

                _percussionFret.gameObject.SetActive(false);
            }

            _fretShowCoroutine = null;
        }
    }
}