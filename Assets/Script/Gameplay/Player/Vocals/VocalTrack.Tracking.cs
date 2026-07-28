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
            NoGap, // Intended for when there is almost no gap between lyrics in the two phrases.
            SmallGap, // Intended for when there is small gap between lyrics in the two phrases, but the phrases are adjacent.
            PhraseToLargeGap,
            LargeGapToPhrase,
            FinalPhraseComplete,
            NoPhrases
        }

        private class StaticPhraseTracker
        {

            public List<VocalsPhrase> Phrases { get; }

            // Index of the phrase that should be leftmost in the static lyrics display. This updates as soon as the last note
            // of a phrase ends, not when the phrase itself ends
            private int _leftmostPhraseIndex = 0;

            private bool _inGap = true;

            // Returns true if it's time to shift
            public StaticLyricShiftType UpdateCurrentPhrase(double time)
            {
                if (Phrases.Count == 0)
                {
                    return StaticLyricShiftType.NoPhrases;
                }

                var currentLeftmostPhrase = Phrases[_leftmostPhraseIndex];

                double shiftTime = currentLeftmostPhrase.TimeEnd;
                if (_leftmostPhraseIndex + 1 < Phrases.Count)
                {
                    const double shiftLeadTime = 0.15;
                    var nextPhrase = Phrases[_leftmostPhraseIndex + 1];
                    if (nextPhrase.Lyrics.Count > 0)
                    {
                        shiftTime = Math.Min(shiftTime, nextPhrase.Lyrics[0].Time - shiftLeadTime);
                    }
                }

                // We haven't passed the last note of the leftmost phrase. If we're in a gap, we need to check if the leftmost phrase
                // is now imminent
                if (_inGap)
                {
                    var startTime = currentLeftmostPhrase.Lyrics.Count > 0 ? currentLeftmostPhrase.Lyrics[0].Time : currentLeftmostPhrase.Time;
                    if (startTime < time + StaticPhraseHelpers.LARGE_GAP_THRESHOLD)
                    {
                        _inGap = false;
                        return StaticLyricShiftType.LargeGapToPhrase;
                    }
                }
                // We've passed the last note of the leftmost phrase, so it's time to shift
                else if (time >= shiftTime)
                {
                    if (_leftmostPhraseIndex + 1 >= Phrases.Count)
                    {
                        return StaticLyricShiftType.FinalPhraseComplete;
                    }

                    _leftmostPhraseIndex++;

                    var newLeftmostPhrase = Phrases[_leftmostPhraseIndex];

                    var timeBetweenLyrics = StaticPhraseHelpers.GetTimeBetweenLyrics(newLeftmostPhrase, currentLeftmostPhrase);

                    // Factor in the shift duration here, so that we don't go from gap to phrase in the middle of a phrase-to-gap shift
                    if (newLeftmostPhrase.Time > time + StaticPhraseHelpers.LARGE_GAP_THRESHOLD + STATIC_LYRIC_SHIFT_DURATION)
                    {
                        _inGap = true;

                        // The next phrase isn't very soon, so shift to a gap
                        return StaticLyricShiftType.PhraseToLargeGap;
                    }

                    // The next phrase is imminent, so shift straight to it
                    return timeBetweenLyrics < StaticPhraseHelpers.SMALL_GAP_THRESHOLD ? StaticLyricShiftType.NoGap : StaticLyricShiftType.SmallGap;
                }



                return StaticLyricShiftType.None;
            }

            public StaticPhraseTracker(List<VocalsPhrase> phrases)
            {
                Phrases = new();
                foreach (var phrase in phrases)
                {
                    if (!phrase.IsPercussion && phrase.Lyrics.Count > 0)
                    {
                        Phrases.Add(phrase);
                    }
                }
            }

            public void Reset()
            {
                _leftmostPhraseIndex = 0;
                _inGap = true;
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
        }
    }
}
