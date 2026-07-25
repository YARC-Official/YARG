using System;
using Cysharp.Text;
using System.Collections.Generic;
using JetBrains.Annotations;
using TMPro;
using UnityEngine;

namespace YARG.Gameplay.Visuals
{
    public class HeldStaticPhraseElement : GameplayBehaviour
    {
        private readonly struct HeldPhrase
        {
            public          double                                        Time    { get; }
            public          double                                        TimeEnd { get; }
            public readonly List<StaticPhraseHelpers.StaticLyricSyllable> Syllables;

            public HeldPhrase(List<StaticPhraseHelpers.StaticLyricSyllable> syllables)
            {
                Syllables = syllables;
                if (Syllables.Count == 0)
                {
                    Time = 0;
                    TimeEnd = 0;
                    return;
                }

                Time = Syllables[0].Time;
                double timeEnd = Time;
                for (int i = 0; i < syllables.Count; i++)
                {
                    var syllable = syllables[i];
                    timeEnd = Math.Max(timeEnd, syllable.TimeEnd);
                }

                TimeEnd = timeEnd;
            }
        }

        private void ClearFinished()
        {
            for (int i = _heldPhrases.Count - 1; i >= 0; i--)
            {
                if (_heldPhrases[i].TimeEnd <= GameManager.VisualTime)
                {
                    _heldPhrases.RemoveAt(i);
                }
            }
        }

        [NotNull]
        private readonly List<HeldPhrase> _heldPhrases = new();
        private int _lastRenderState = int.MinValue;

        private Utf16ValueStringBuilder _builder;

        [SerializeField]
        private TextMeshPro _phraseText;

        public void Initialize()
        {
            _builder = ZString.CreateStringBuilder(false);
            _phraseText.text = string.Empty;
            _lastRenderState = int.MinValue;
        }

        public void AddSyllables(List<StaticPhraseHelpers.StaticLyricSyllable> syllables)
        {
            _lastRenderState = int.MinValue;
            _heldPhrases.Add(new HeldPhrase(syllables));
            _builder.Clear();
        }

        private void Update()
        {
            ClearFinished();

            var renderState = GetRenderState();
            if (renderState == _lastRenderState)
            {
                return;
            }

            _lastRenderState = renderState;
            UpdateText();
        }

        private void UpdateText()
        {
            _builder.Clear();
            for (int i = 0; i < _heldPhrases.Count; i++)
            {
                var phrase = _heldPhrases[i];
                for (int j = 0; j < phrase.Syllables.Count; j++)
                {
                    var syllable = phrase.Syllables[j];
                    if (GameManager.VisualTime < syllable.Time)
                    {
                        StaticPhraseHelpers.BuilderAppendWithColorTag(syllable.Text,
                            syllable.IsStarpower
                                ? StaticPhraseHelpers.FUTURE_STAR_POWER_LYRIC_COLOR_TAG
                                : StaticPhraseHelpers.FUTURE_LYRIC_COLOR_TAG, ref _builder);
                    }
                    else if (syllable.Time <= GameManager.VisualTime && GameManager.VisualTime < syllable.TimeEnd)
                    {
                        StaticPhraseHelpers.BuilderAppendWithColorTag(syllable.Text,
                            StaticPhraseHelpers.PRESENT_LYRIC_COLOR_TAG, ref _builder);
                    }
                    else
                    {
                        StaticPhraseHelpers.BuilderAppendWithColorTag(syllable.Text,
                            syllable.IsStarpower
                                ? StaticPhraseHelpers.PAST_STAR_POWER_LYRIC_COLOR_TAG
                                : StaticPhraseHelpers.PAST_LYRIC_COLOR_TAG, ref _builder);
                    }
                }

                if (i < _heldPhrases.Count - 1)
                {
                    _builder.Append(' ');
                }
            }

            _phraseText.SetText(_builder);
        }

        private int GetRenderState()
        {
            var hash = new HashCode();
            for (int i = 0; i < _heldPhrases.Count; i++)
            {
                var phrase = _heldPhrases[i];
                StaticPhraseHelpers.AddToRenderState(phrase.Syllables, GameManager.VisualTime, ref hash);
            }

            return hash.ToHashCode();
        }
    }
}