using System;
using Cysharp.Text;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using TMPro;
using UnityEngine;

namespace YARG.Gameplay.Visuals
{
    public class
        HeldStaticPhraseElement : GameplayBehaviour // not a VocalElement because it doesn't scroll along the highway
    {
        private const string PAST_LYRIC_COLOR_TAG              = "<color=#595959>";
        private const string PAST_STAR_POWER_LYRIC_COLOR_TAG   = "<color=#757519>";
        private const string PRESENT_LYRIC_COLOR_TAG           = "<color=#13f0a6>";
        private const string FUTURE_LYRIC_COLOR_TAG            = "<color=#FFFFFF>";
        private const string FUTURE_STAR_POWER_LYRIC_COLOR_TAG = "<color=#FFEB04>";
        private const string CLOSE_COLOR_TAG                   = "</color>";

        private void ClearFinished()
        {
            int i = 0;
            while (i < _syllables.Count)
            {
                var syllable = _syllables[i];
                if (syllable.TimeEnd <= GameManager.VisualTime)
                {
                    _syllables.RemoveAt(i);
                }
                else
                {
                    i++;
                }
            }
        }

        private bool IsAllComplete()
        {
            foreach (var syllable in _syllables)
            {
                if (syllable.TimeEnd > GameManager.VisualTime)
                {
                    return false;
                }
            }

            return true;
        }

        [NotNull]
        private readonly List<VocalStaticLyricPhraseElement.StaticLyricSyllable> _syllables = new();
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

        public void AddSyllables(List<VocalStaticLyricPhraseElement.StaticLyricSyllable> syllables)
        {
            _lastRenderState = int.MinValue;
            _syllables.AddRange(syllables);
            ClearFinished();
            _builder.Clear();
        }

        private void Update()
        {
            if (IsAllComplete())
            {
                _syllables.Clear();
                _builder.Clear();
                _phraseText.text = string.Empty;
                _lastRenderState = int.MinValue;
                return;
            }

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

            foreach (var syllable in _syllables)
            {
                if (GameManager.VisualTime < syllable.Time)
                {
                    BuilderAppendWithColorTag(syllable.Text,
                        syllable.IsStarpower ? FUTURE_STAR_POWER_LYRIC_COLOR_TAG : FUTURE_LYRIC_COLOR_TAG);
                }
                else if (syllable.Time <= GameManager.VisualTime && GameManager.VisualTime < syllable.TimeEnd)
                {
                    BuilderAppendWithColorTag(syllable.Text, PRESENT_LYRIC_COLOR_TAG);
                }
                else
                {
                    BuilderAppendWithColorTag(syllable.Text,
                        syllable.IsStarpower ? PAST_STAR_POWER_LYRIC_COLOR_TAG : PAST_LYRIC_COLOR_TAG);
                }
            }

            _phraseText.text = _builder.ToString();
        }

        private int GetRenderState()
        {
            var hash = new HashCode();

            for (int i = 0; i < _syllables.Count; i++)
            {
                var syllable = _syllables[i];
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
    }
}