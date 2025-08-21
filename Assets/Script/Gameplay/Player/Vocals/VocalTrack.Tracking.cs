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
        public enum PhraseChangeType
        {
            NoChange,
            ExitedPhrase, // Exited into a gap between phrases
            EnteredPhrase, // Entered a phrase from a gap
            ExitedAndEnteredPhrase // Went directly from one phrase to the next
        }

        public struct PhraseChangeInfo
        {
            public PhraseChangeType Type;
            public int? PhraseExitedIdx;
            public int? PhraseEnteredIdx;
        }

        private class StaticPhraseTracker
        {
            private readonly VocalsPart _vocalsPart;

            // These track the index of the phrase and note that were most recently *started*, regardless
            // of whether they have since ended. Before we hit the first phrase or note (index 0), default
            // to -1
            private int _phraseIndex = -1;

            // These track whether the indexed phrase and note are currently being played (true), or if we're instead
            // in a gap between that phrase/note and the next (false)
            private bool _phraseIndexStillActive = false;

            public VocalsPhrase? CurrentPhrase => _phraseIndexStillActive ? _vocalsPart.NotePhrases[_phraseIndex] : null;

            public IEnumerable<VocalsPhrase> FuturePhrases => _vocalsPart.NotePhrases.Skip(_phraseIndex + 1);

            public IEnumerable<VocalsPhrase> CurrentAndFuturePhrases => _vocalsPart.NotePhrases.Skip(_phraseIndex);

            // Returns true if the current phrase has changed (either because there's a new index or because we're now between phrases)
            public PhraseChangeInfo UpdateCurrentPhrase(double time)
            {
                for (var i = 0; i < _vocalsPart.NotePhrases.Count; i++)
                {
                    var phrase = _vocalsPart.NotePhrases[i];

                    if (phrase.TimeEnd < time)
                    {
                        // Phrases that have already passed are irrelevant
                        continue;
                    }

                    // We've reached a phrase that has not yet ended. Now check if it's begun yet
                    if (phrase.Time <= time)
                    {
                        // This phrase has begun and not yet ended; it's the current phrase...

                        if (_phraseIndex == i)
                        {
                            // ...but we knew that already; no change
                            return new() { Type = PhraseChangeType.NoChange };
                        }

                        // ...which is new since the last time we checked. Update the phrase index
                        _phraseIndex = i;

                        // Did we come from another phrase?
                        if (_phraseIndexStillActive)
                        {
                            // Yes
                            return new() { Type = PhraseChangeType.ExitedAndEnteredPhrase, PhraseEnteredIdx = i, PhraseExitedIdx = i - 1 };
                        }
                        // No; we came from a gap
                        _phraseIndexStillActive = true;
                        return new() { Type = PhraseChangeType.EnteredPhrase, PhraseEnteredIdx = i };
                    }

                    // We're in a phrase gap...
                    if (!_phraseIndexStillActive)
                    {
                        // ...but we already knew that; no change
                        return new() { Type = PhraseChangeType.NoChange };
                    }
                    // ...which is new since the last time we checked
                    _phraseIndexStillActive = false;
                    return new() { Type = PhraseChangeType.ExitedPhrase, PhraseExitedIdx = i };
                    
                }

                // We didn't find any phrases, presumably because we've passed the last phrase of the song. Nothing has changed
                return new() { Type = PhraseChangeType.NoChange };
            }

            public StaticPhraseTracker(VocalsPart vocalsPart)
            {
                _vocalsPart = vocalsPart;
            }

            public void Reset()
            {
                _phraseIndex = -1;
                _phraseIndexStillActive = false;
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
