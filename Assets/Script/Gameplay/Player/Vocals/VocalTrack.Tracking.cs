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
        public enum StaticLyricShiftType
        {
            None,
            PhraseToPhrase,
            PhraseToGap,
            GapToPhrase,
            FinalPhraseComplete
        }

        private class StaticPhraseTracker
        {
            private const double IMMINENCE_THRESHOLD = .3d;

            private readonly VocalsPart _vocalsPart;

            // Index of the the phrase that should be leftmost in the static lyrics display. This updates as soon as the last note
            // of a phrase ends, not when the phrase itself ends
            private int _leftmostPhraseIndex = 0;

            private bool _inGap = true;

            // Returns true if it's time to shift
            public StaticLyricShiftType UpdateCurrentPhrase(double time)
            {
                var currentLeftmostPhrase = _vocalsPart.NotePhrases[_leftmostPhraseIndex];

                // We haven't passed the last note of the leftmost phrase. If we're in a gap, we need to check if the leftmost phrase
                // is now imminent
                if (_inGap)
                {
                    if (currentLeftmostPhrase.PhraseParentNote.Time < time + IMMINENCE_THRESHOLD)
                    {
                        _inGap = false;
                        return StaticLyricShiftType.GapToPhrase;
                    }
                }

                // We've passed the last note of the leftmost phrase, so it's time to shift
                else if (time >= currentLeftmostPhrase.PhraseParentNote.ChildNotes[^1].TotalTimeEnd)
                {
                    _leftmostPhraseIndex++;

                    if (_leftmostPhraseIndex >= _vocalsPart.NotePhrases.Count)
                    {
                        return StaticLyricShiftType.FinalPhraseComplete;
                    }

                    var newLeftmostPhrase = _vocalsPart.NotePhrases[_leftmostPhraseIndex];

                    // Factor in the shift duration here, so that we don't go from gap to phrase in the middle of a phrase-to-gap shift
                    if (newLeftmostPhrase.PhraseParentNote.Time > time + IMMINENCE_THRESHOLD + STATIC_LYRIC_SHIFT_DURATION)
                    {
                        _inGap = true;

                        // The next phrase isn't very soon, so shift to a gap
                        return StaticLyricShiftType.PhraseToGap;
                    }

                    // The next phrase is imminent, so shift straight to it
                    return StaticLyricShiftType.PhraseToPhrase;
                }

                

                return StaticLyricShiftType.None;
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
