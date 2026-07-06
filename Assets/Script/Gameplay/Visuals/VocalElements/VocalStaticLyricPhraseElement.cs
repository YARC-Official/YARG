using System;
using Cysharp.Text;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Gameplay.Player;
using static YARG.Gameplay.Player.VocalTrack;

namespace YARG.Gameplay.Visuals
{
    public class VocalStaticLyricPhraseElement : BaseElement // not a VocalElement because it doesn't scroll along the highway
    {
        private const string PAST_LYRIC_COLOR_TAG = "<color=#595959>";
        private const string PAST_STAR_POWER_LYRIC_COLOR_TAG = "<color=#757519>";
        private const string PRESENT_LYRIC_COLOR_TAG = "<color=#13f0a6>";
        private const string FUTURE_LYRIC_COLOR_TAG = "<color=#FFFFFF>";
        private const string FUTURE_STAR_POWER_LYRIC_COLOR_TAG = "<color=#FFEB04>";
        private const string FUTURE_PHRASE_COLOR_TAG = "<color=#595959>";
        private const string FUTURE_STAR_POWER_PHRASE_COLOR_TAG = "<color=#757519>";
        private const string CLOSE_COLOR_TAG = "</color>";

        public sealed class PreparedPhrase
        {
            public readonly VocalPhrasePair PhrasePair;
            public readonly List<StaticLyricSyllable> Syllables;
            public readonly string FutureText;
            public readonly float Width;
            public readonly double Duration;

            public PreparedPhrase(VocalPhrasePair phrasePair, List<VocalsPhrase> scoringPhrases,
                bool allowHiding, float width)
            {
                PhrasePair = phrasePair;
                Duration = phrasePair.Duration;
                Syllables = BuildSyllables(phrasePair, scoringPhrases, allowHiding);
                FutureText = BuildFutureText(Syllables);
                Width = width;
            }

            private PreparedPhrase(VocalPhrasePair phrasePair, double duration,
                List<StaticLyricSyllable> syllables, string futureText, float width)
            {
                PhrasePair = phrasePair;
                Duration = duration;
                Syllables = syllables;
                FutureText = futureText;
                Width = width;
            }

            public PreparedPhrase WithWidth(float width)
            {
                return new PreparedPhrase(PhrasePair, Duration, Syllables, FutureText, width);
            }
        }

        public readonly struct StaticLyricSyllable
        {
            public readonly string Text;
            public readonly double Time;
            public readonly double TimeEnd;
            public readonly bool IsStarpower;

            public StaticLyricSyllable(string text, double time, double timeEnd, bool isStarpower,
                LyricSymbolFlags flags, bool isLastLyricOfPhrase)
            {
                var builder = ZString.CreateStringBuilder(false);

                Time = time;
                TimeEnd = timeEnd;
                IsStarpower = isStarpower;

                if ((flags & LyricSymbolFlags.NonPitched) != 0)
                {
                    builder.Append("<i>");
                }

                if ((flags & LyricSymbolFlags.JoinWithNext) != 0)
                {
                    builder.Append(text[0..^1]);
                }
                else
                {
                    builder.Append(text);
                }

                if ((flags & LyricSymbolFlags.NonPitched) != 0)
                {
                    builder.Append("</i>");
                }

                if (!isLastLyricOfPhrase && (flags & LyricSymbolFlags.JoinWithNext) == 0 &&
                    (flags & LyricSymbolFlags.HyphenateWithNext) == 0)
                {
                    builder.Append(" ");
                }

                Text = builder.ToString();
            }
        }

        private PreparedPhrase _preparedPhrase;
        private float _x;
        private bool _isFuture = true;
        private int _lastRenderState = int.MinValue;

        private Utf16ValueStringBuilder _builder;

        public override double ElementTime => _preparedPhrase.PhrasePair.Time;

        [SerializeField]
        private TextMeshPro _phraseText;

        public float Width => _preparedPhrase.Width;

        public double Duration => _preparedPhrase.Duration;

        public void Initialize(PreparedPhrase preparedPhrase, float x)
        {
            _preparedPhrase = preparedPhrase;
            _x = x;
            _isFuture = true;
            _builder = ZString.CreateStringBuilder(false);
            _lastRenderState = int.MinValue;
        }

        protected override void InitializeElement()
        {
            transform.localPosition = transform.localPosition.WithX(_x);
            _phraseText.text = _preparedPhrase.FutureText;
        }

        public void Activate()
        {
            _isFuture = false;
            _lastRenderState = int.MinValue;
        }

        public void Dismiss()
        {
            _isFuture = true;
            _lastRenderState = int.MinValue;
            _builder.Clear();
            DisableIntoPool();
            ParentPool.Return(this);
        }

        protected override void UpdateElement()
        {
            if (_isFuture)
            {
                return;
            }

            var renderState = GetRenderState();
            if (renderState == _lastRenderState)
            {
                return;
            }

            _lastRenderState = renderState;
            _builder.Clear();

            foreach (var syllable in _preparedPhrase.Syllables)
            {
                if (GameManager.VisualTime < syllable.Time)
                {
                    BuilderAppendWithColorTag(syllable.Text, syllable.IsStarpower ? FUTURE_STAR_POWER_LYRIC_COLOR_TAG : FUTURE_LYRIC_COLOR_TAG);
                }
                else if (syllable.Time <= GameManager.VisualTime && GameManager.VisualTime < syllable.TimeEnd)
                {
                    BuilderAppendWithColorTag(syllable.Text, PRESENT_LYRIC_COLOR_TAG);
                }
                else {
                    BuilderAppendWithColorTag(syllable.Text, syllable.IsStarpower ? PAST_STAR_POWER_LYRIC_COLOR_TAG : PAST_LYRIC_COLOR_TAG);
                }
            }

            _phraseText.text = _builder.ToString();
        }

        protected override bool UpdateElementPosition()
        {
            return true;
        }

        protected override void HideElement()
        {
        }

        private int GetRenderState()
        {
            var hash = new HashCode();

            for (int i = 0; i < _preparedPhrase.Syllables.Count; i++)
            {
                var syllable = _preparedPhrase.Syllables[i];
                int state = 2; // syllable is already hit (gray)

                if (GameManager.VisualTime < syllable.Time)
                {
                    state = 0; // syllable is in current phrase (active/white)
                }
                else if (GameManager.VisualTime < syllable.TimeEnd)
                {
                    state = 1; // syllable is being hit (cyan)
                }

                hash.Add(state);

                if (state == 0)
                {
                    // We can reasonably assume if we run into a syllable that has not yet been hit,
                    // there is no change after that syllable.
                    break;
                }
            }

            return hash.ToHashCode();
        }

        private void BuilderAppendWithColorTag(string text, string colorTag)
        {
            _builder.Append(colorTag);
            _builder.Append(text);
            _builder.Append(CLOSE_COLOR_TAG);
        }

        private static List<StaticLyricSyllable> BuildSyllables(VocalPhrasePair phrasePair,
            List<VocalsPhrase> scoringPhrases, bool allowHiding)
        {
            var syllables = new List<StaticLyricSyllable>();
            var mergedLyricIdx = 0;

            var mainPhrase = phrasePair.MainPhrase;
            var mergedPhrase = phrasePair.MergedPhrase;

            // Handle HARM3-only phrases
            if (mainPhrase is null)
            {
                while (mergedLyricIdx < mergedPhrase.Lyrics.Count)
                {
                    var isLastLyricOfMergedPhrase = mergedLyricIdx == mergedPhrase.Lyrics.Count - 1;

                    var mergedLyric = mergedPhrase.Lyrics[mergedLyricIdx++];
                    var probableMergedLyricEnd = GetProbableNoteEndOfLyric(mergedPhrase, mergedLyric);

                    if (probableMergedLyricEnd is null)
                    {
                        continue;
                    }

                    MakeStaticLyricSyllable(syllables, scoringPhrases, allowHiding, mergedLyric.Text,
                        mergedLyric.Time, probableMergedLyricEnd.Value, mergedLyric.Flags, isLastLyricOfMergedPhrase);
                }
            }
            else
            {
                for (var mainLyricIdx = 0; mainLyricIdx < mainPhrase.Lyrics.Count; mainLyricIdx++)
                {
                    var mainLyric = mainPhrase.Lyrics[mainLyricIdx];
                    var probableMainLyricEnd = GetProbableNoteEndOfLyric(mainPhrase, mainLyric);
                    if (probableMainLyricEnd is null)
                    {
                        continue;
                    }

                    var isLastLyricOfMainPhrase = mainLyricIdx == mainPhrase.Lyrics.Count - 1;

                    if (mergedPhrase is not null)
                    {
                        // Handle any merged lyrics that happened before the current lyric
                        while (mergedLyricIdx < mergedPhrase.Lyrics.Count)
                        {
                            if (mergedPhrase.Lyrics[mergedLyricIdx].Time >= mainPhrase.Lyrics[mainLyricIdx].Time)
                            {
                                break;
                            }

                            var mergedLyric = mergedPhrase.Lyrics[mergedLyricIdx++];
                            var probableMergedLyricEnd = GetProbableNoteEndOfLyric(mergedPhrase, mergedLyric);
                            if (probableMergedLyricEnd is null)
                            {
                                continue;
                            }

                            // isLastLyricOfPhrase is definitely false, because we still have at least one main phrase lyric to add
                            MakeStaticLyricSyllable(syllables, scoringPhrases, allowHiding, mergedLyric.Text,
                                mergedLyric.Time, probableMergedLyricEnd.Value, mergedLyric.Flags, false);
                        }
                    }

                    bool mainLyricIsLastLyricOfEntirePhrase; // Including both the main and merged lyrics
                    if (isLastLyricOfMainPhrase)
                    {
                        // This is the last lyric of the main phrase, but what about the merged phrase?
                        mainLyricIsLastLyricOfEntirePhrase = mergedPhrase is not null && mergedLyricIdx < mergedPhrase.Lyrics.Count - 1;
                    } else
                    {
                        // This isn't even the last lyric of the main phrase, so it's definitely not the last one overall
                        mainLyricIsLastLyricOfEntirePhrase = false;
                    }

                    MakeStaticLyricSyllable(syllables, scoringPhrases, allowHiding, mainLyric.Text,
                        mainLyric.Time, probableMainLyricEnd.Value, mainLyric.Flags, mainLyricIsLastLyricOfEntirePhrase);

                    // If there's a simultaneous syllable in the merged part...
                    if (mergedPhrase is not null && mergedLyricIdx < mergedPhrase.Lyrics.Count && mergedPhrase.Lyrics[mergedLyricIdx].Time == mainLyric.Time)
                    {
                        var simultaneousMergedLyric = mergedPhrase.Lyrics[mergedLyricIdx++];

                        // ...and its text isn't an exact match to the main syllable...
                        if (simultaneousMergedLyric.Text != mainLyric.Text)
                        {
                            var probableSimultaneousMergedLyricEnd = GetProbableNoteEndOfLyric(mergedPhrase, simultaneousMergedLyric);

                            if (probableSimultaneousMergedLyricEnd is not null)
                            {
                                var isLastLyricOfMergedPhrase = mergedLyricIdx == mergedPhrase.Lyrics.Count - 1;

                                // ...add it after the main syllable
                                MakeStaticLyricSyllable(
                                    syllables,
                                    scoringPhrases,
                                    allowHiding,
                                    simultaneousMergedLyric.Text,
                                    simultaneousMergedLyric.Time,
                                    probableSimultaneousMergedLyricEnd.Value,
                                    simultaneousMergedLyric.Flags,
                                    mainLyricIsLastLyricOfEntirePhrase && mergedLyricIdx == mergedPhrase.Lyrics.Count - 1
                                );
                            }
                        }
                    }
                }

                // Handle any remaining merged lyrics after the last main phrase lyric
                if (mergedPhrase is not null)
                {
                    while (mergedLyricIdx < mergedPhrase.Lyrics.Count)
                    {
                        var mergedLyric = mergedPhrase.Lyrics[mergedLyricIdx++];
                        var probableMergedLyricEnd = GetProbableNoteEndOfLyric(mergedPhrase, mergedLyric);
                        if (probableMergedLyricEnd is null)
                        {
                            continue;
                        }

                        var isLastLyricOfMergedPhrase = mergedLyricIdx == mergedPhrase.Lyrics.Count - 1;
                        MakeStaticLyricSyllable(syllables, scoringPhrases, allowHiding, mergedLyric.Text,
                            mergedLyric.Time, probableMergedLyricEnd.Value, mergedLyric.Flags, isLastLyricOfMergedPhrase);
                    }
                }
            }

            return syllables;
        }

        private static double? GetProbableNoteEndOfLyric(VocalsPhrase phrase, LyricEvent lyric)
        {
            return phrase.PhraseParentNote.ChildNotes
                .FirstOrDefault(note => note.Tick == lyric.Tick)?.TotalTimeEnd;
        }

        private static void MakeStaticLyricSyllable(List<StaticLyricSyllable> syllables,
            List<VocalsPhrase> scoringPhrases, bool allowHiding, string text, double time, double timeEnd,
            LyricSymbolFlags flags, bool isLastLyricOfPhrase)
        {
            if (allowHiding && ((flags & LyricSymbolFlags.HarmonyHidden) != 0))
            {
                return;
            }

            // Determine whether the lyric falls within a star power scoring phrase
            var isStarpower = false;
            foreach (var scoringPhrase in scoringPhrases)
            {
                if (scoringPhrase.Time > time)
                {
                    // We've reached the scoring phrase past this lyric, so we can stop
                    // Arguably belt-and-suspenders, because the lyric should definitely be in *some* phrase, and we break once we find it
                    break;
                }

                if (scoringPhrase.TimeEnd <= time)
                {
                    // This phrase ends before the lyric, so is irrelevant to whether the lyric is star power
                    continue;
                }

                // At this point, we've found the scoring phrase that this lyric is a part of (going off of the beginning of the lyric)
                isStarpower = scoringPhrase.IsStarPower;
            }

            syllables.Add(new(text, time, timeEnd, isStarpower, flags, isLastLyricOfPhrase));
        }

        private static string BuildFutureText(List<StaticLyricSyllable> syllables)
        {
            var builder = ZString.CreateStringBuilder(false);
            foreach (var syllable in syllables)
            {
                builder.Append(syllable.IsStarpower ? FUTURE_STAR_POWER_PHRASE_COLOR_TAG : FUTURE_PHRASE_COLOR_TAG);
                builder.Append(syllable.Text);
                builder.Append(CLOSE_COLOR_TAG);
            }

            return builder.ToString();
        }
    }
}
