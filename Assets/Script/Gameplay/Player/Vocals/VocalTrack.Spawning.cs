using System.Linq;
using YARG.Core.Chart;
using YARG.Gameplay.Visuals;

namespace YARG.Gameplay.Player
{
    public partial class VocalTrack
    {
        private class PhraseNoteTracker
        {
            private readonly VocalsPart _vocalsPart;

            private int _phraseIndex;
            private int _noteOrLyricIndex;

            public VocalsPhrase CurrentPhrase => _vocalsPart.NotePhrases[_phraseIndex];
            private bool CurrentPhraseInBounds => _phraseIndex < _vocalsPart.NotePhrases.Count;

            public VocalNote CurrentNote =>
                CurrentPhrase.PhraseParentNote.ChildNotes[_noteOrLyricIndex];
            public bool CurrentNoteInBounds =>
                CurrentPhraseInBounds &&
                _noteOrLyricIndex < CurrentPhrase.PhraseParentNote.ChildNotes.Count;

            public LyricEvent CurrentLyric =>
                CurrentPhrase.Lyrics[_noteOrLyricIndex];
            public bool CurrentLyricInBounds =>
                CurrentPhraseInBounds &&
                _noteOrLyricIndex < CurrentPhrase.Lyrics.Count;

            public PhraseNoteTracker(VocalsPart vocalsPart, bool forLyrics)
            {
                _vocalsPart = vocalsPart;

                // If the first phrase in the song has no notes/lyrics, skip it
                if (CurrentPhraseInBounds)
                {
                    if (forLyrics && !CurrentLyricInBounds)
                    {
                        NextLyric();
                    }
                    else if (!forLyrics && !CurrentNoteInBounds)
                    {
                        NextNote();
                    }
                }
            }

            public void Reset()
            {
                _phraseIndex = 0;
                _noteOrLyricIndex = 0;
            }

            public void NextNote()
            {
                _noteOrLyricIndex++;

                if (CurrentNoteInBounds) return;

                // Make sure to skip all of the empty phrases
                do
                {
                    _phraseIndex++;
                    _noteOrLyricIndex = 0;
                } while (CurrentPhraseInBounds && !CurrentNoteInBounds);
            }

            public void NextLyric()
            {
                _noteOrLyricIndex++;

                if (CurrentLyricInBounds) return;

                // Make sure to skip all of the empty phrases
                do
                {
                    _phraseIndex++;
                    _noteOrLyricIndex = 0;
                } while (CurrentPhraseInBounds && !CurrentLyricInBounds);
            }

            public VocalNote GetProbableNoteAtLyric()
            {
                return CurrentPhrase.PhraseParentNote.ChildNotes
                    .FirstOrDefault(note => note.Tick == CurrentLyric.Tick);
            }
        }

        private int[] _phraseMarkerIndices;

        // These track the phrase and note within the phrase
        private PhraseNoteTracker[] _noteTrackers;
        private PhraseNoteTracker[] _lyricTrackers;

        private void UpdateSpawning()
        {
            // For each harmony...
            for (int i = 0; i < _vocalsTrack.Parts.Count; i++)
            {
                // Spawn in notes and lyrics
                SpawnNotesInPhrase(_noteTrackers[i], i);
                SpawnLyricsInPhrase(_lyricTrackers[i], i);
                SpawnPhraseLines(i);
            }
        }

        private void SpawnNotesInPhrase(PhraseNoteTracker tracker, int harmonyIndex)
        {
            var pool = _notePools[harmonyIndex];

            while (tracker.CurrentNoteInBounds && tracker.CurrentNote.Time <= GameManager.SongTime + SpawnTimeOffset)
            {
                var note = tracker.CurrentNote;

                if (note.IsNonPitched)
                {
                    // Skip this frame if the pool is full
                    if (!_talkiePool.CanSpawnAmount(1))
                    {
                        return;
                    }

                    // Spawn the vocal note
                    var noteObj = _talkiePool.TakeWithoutEnabling();
                    ((VocalTalkieElement) noteObj).NoteRef = note;
                    noteObj.EnableFromPool();
                }
                else if (!note.IsPercussion)
                {
                    // Skip this frame if the pool is full
                    if (!pool.CanSpawnAmount(1))
                    {
                        return;
                    }

                    // Spawn the vocal note
                    var noteObj = pool.TakeWithoutEnabling();
                    ((VocalNoteElement) noteObj).NoteRef = note;
                    noteObj.EnableFromPool();
                }

                tracker.NextNote();
            }
        }

        private void SpawnLyricsInPhrase(PhraseNoteTracker tracker, int harmonyIndex)
        {
            while (tracker.CurrentLyricInBounds && tracker.CurrentLyric.Time <= GameManager.SongTime + SpawnTimeOffset)
            {
                if (!_lyricContainer.TrySpawnLyric(
                    tracker.CurrentLyric,
                    tracker.GetProbableNoteAtLyric(),
                    AllowStarPower && tracker.CurrentPhrase.IsStarPower,
                    harmonyIndex))
                {
                    tracker.NextLyric();
                    return;
                }

                tracker.NextLyric();
            }
        }

        private void SpawnPhraseLines(int harmonyIndex)
        {
            var phrases = _vocalsTrack.Parts[harmonyIndex].NotePhrases;
            int index = _phraseMarkerIndices[harmonyIndex];

            while (index < phrases.Count && phrases[index].TimeEnd <= GameManager.SongTime + SpawnTimeOffset)
            {
                // Spawn the phrase end line
                var poolable = _phraseLinePool.TakeWithoutEnabling();
                ((PhraseLineElement) poolable).PhraseRef = phrases[index];
                poolable.EnableFromPool();

                index++;
            }

            // Update the index value
            _phraseMarkerIndices[harmonyIndex] = index;
        }
    }
}