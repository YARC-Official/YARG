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

        public sealed class PreparedPhrase
        {
            public readonly VocalsPhrase                                  Phrase;
            public readonly List<StaticPhraseHelpers.StaticLyricSyllable> Syllables;
            public readonly string                                        FutureText;
            public readonly float                                         Width;
            public readonly double                                        Duration;

            public PreparedPhrase(VocalsPhrase phrase, List<VocalsPhrase> scoringPhrases,
                bool allowHiding, float width)
            {
                Phrase = phrase;
                Duration = phrase.TimeLength;
                Syllables = BuildSyllables(phrase, scoringPhrases, allowHiding);
                FutureText = BuildFutureText(Syllables);
                Width = width;
            }

            private PreparedPhrase(VocalsPhrase phrase, double duration,
                List<StaticPhraseHelpers.StaticLyricSyllable> syllables, string futureText, float width)
            {
                Phrase = phrase;
                Duration = duration;
                Syllables = syllables;
                FutureText = futureText;
                Width = width;
            }

            public PreparedPhrase WithWidth(float width)
            {
                return new PreparedPhrase(Phrase, Duration, Syllables, FutureText, width);
            }
        }

        private PreparedPhrase _preparedPhrase;
        private float _x;
        private bool _isFuture = true;
        private int _lastRenderState = int.MinValue;

        private Utf16ValueStringBuilder _builder;

        public override double ElementTime => _preparedPhrase.Phrase.Time;

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

        public List<StaticPhraseHelpers.StaticLyricSyllable> Dismiss()
        {
            var result = new List<StaticPhraseHelpers.StaticLyricSyllable>();
            foreach (var syllable in _preparedPhrase.Syllables)
            {
                if (GameManager.VisualTime < syllable.TimeEnd)
                {
                    result.Add(syllable);
                }
            }
            _isFuture = true;
            _lastRenderState = int.MinValue;
            _builder.Clear();
            DisableIntoPool();
            ParentPool.Return(this);
            return result;
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
                    StaticPhraseHelpers.BuilderAppendWithColorTag(syllable.Text, syllable.IsStarpower ? StaticPhraseHelpers.FUTURE_STAR_POWER_LYRIC_COLOR_TAG : StaticPhraseHelpers.FUTURE_LYRIC_COLOR_TAG, ref _builder);
                }
                else if (syllable.Time <= GameManager.VisualTime && GameManager.VisualTime < syllable.TimeEnd)
                {
                    StaticPhraseHelpers.BuilderAppendWithColorTag(syllable.Text, StaticPhraseHelpers.PRESENT_LYRIC_COLOR_TAG, ref _builder);
                }
                else {
                    StaticPhraseHelpers.BuilderAppendWithColorTag(syllable.Text, syllable.IsStarpower ? StaticPhraseHelpers.PAST_STAR_POWER_LYRIC_COLOR_TAG : StaticPhraseHelpers.PAST_LYRIC_COLOR_TAG, ref _builder);
                }
            }

            _phraseText.SetText(_builder);
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
            StaticPhraseHelpers.AddToRenderState(_preparedPhrase.Syllables, GameManager.VisualTime, ref hash);
            return hash.ToHashCode();
        }

        private static List<StaticPhraseHelpers.StaticLyricSyllable> BuildSyllables(VocalsPhrase phrase,
            List<VocalsPhrase> scoringPhrases, bool allowHiding)
        {
            var syllables = new List<StaticPhraseHelpers.StaticLyricSyllable>();
            var mergedLyricIdx = 0;

            // Handle HARM3-only phrases
            while (mergedLyricIdx < phrase.Lyrics.Count)
            {
                var isLastLyricOfMergedPhrase = mergedLyricIdx == phrase.Lyrics.Count - 1;

                var mergedLyric = phrase.Lyrics[mergedLyricIdx++];

                MakeStaticLyricSyllable(syllables, scoringPhrases, allowHiding, mergedLyric.Text,
                    mergedLyric.Time, mergedLyric.TimeEnd, mergedLyric.Flags, isLastLyricOfMergedPhrase);
            }

            return syllables;
        }

        private static void MakeStaticLyricSyllable(List<StaticPhraseHelpers.StaticLyricSyllable> syllables,
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

        private static string BuildFutureText(List<StaticPhraseHelpers.StaticLyricSyllable> syllables)
        {
            var builder = ZString.CreateStringBuilder(false);
            foreach (var syllable in syllables)
            {
                builder.Append(syllable.IsStarpower ? StaticPhraseHelpers.FUTURE_STAR_POWER_PHRASE_COLOR_TAG : StaticPhraseHelpers.FUTURE_PHRASE_COLOR_TAG);
                builder.Append(syllable.Text);
                builder.Append(StaticPhraseHelpers.CLOSE_COLOR_TAG);
            }

            return builder.ToString();
        }
    }
}
