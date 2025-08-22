using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YARG.Core.Chart;
using YARG.Gameplay.Visuals;

namespace YARG.Gameplay.Player
{
    public partial class VocalTrack
    {

        private class StaticPhraseTracker
        {

            private readonly VocalsPart _vocalsPart;

            // Index of the the phrase that should be leftmost in the static lyrics display. This updates as soon as the last note
            // of a phrase ends, not when the phrase itself ends
            private int _leftmostPhraseIndex = 0;

            // Returns true if it's time to shift
            public bool UpdateCurrentPhrase(double time)
            {
                var currentLeftmostPhrase = _vocalsPart.NotePhrases[_leftmostPhraseIndex];

                if (time >= currentLeftmostPhrase.PhraseParentNote.ChildNotes[^1].TotalTimeEnd)
                {
                    _leftmostPhraseIndex++;
                    return true;
                }

                return false;
            }

            public StaticPhraseTracker(VocalsPart vocalsPart)
            {
                _vocalsPart = vocalsPart;
            }

            public void Reset()
            {
                _leftmostPhraseIndex = 0;
            }
        }

        private class ScrollingPhraseNoteTracker
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

            public ScrollingPhraseNoteTracker(VocalsPart vocalsPart, bool forLyrics)
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
    }
}
