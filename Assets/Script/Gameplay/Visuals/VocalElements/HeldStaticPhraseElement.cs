using System;
using Cysharp.Text;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using static YARG.Gameplay.Visuals.StaticPhraseHelpers;

namespace YARG.Gameplay.Visuals
{
    public class HeldStaticPhraseElement : GameplayBehaviour
    {
        private readonly struct HeldPhrase
        {
            public          double                    Time    { get; }
            public          double                    TimeEnd { get; }
            public readonly List<StaticLyricSyllable> Syllables;

            public HeldPhrase(List<StaticLyricSyllable> syllables, double visualTime)
            {
                Syllables = syllables;
                for (int i = Syllables.Count - 1; i >= 0; i--)
                {
                    var syllable = Syllables[i];
                    if (syllable.TimeEnd < visualTime)
                    {
                        Syllables.RemoveAt(i);
                    }
                }

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

        private readonly List<HeldPhrase> _heldPhrases     = new();
        private          int              _lastRenderState = int.MinValue;

        private Utf16ValueStringBuilder _builder;

        [SerializeField]
        private TextMeshPro _phraseText;

        public void Initialize()
        {
            _builder = ZString.CreateStringBuilder(false);
            _phraseText.text = string.Empty;
            _lastRenderState = int.MinValue;
        }

        public void AddSyllables(List<StaticLyricSyllable> syllables)
        {
            var phrase = new HeldPhrase(syllables, GameManager.VisualTime);
            if (phrase.Syllables.Count == 0)
            {
                return;
            }
            _heldPhrases.Add(phrase);
            _lastRenderState = int.MinValue;
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
                AddSyllablesToBuilder(phrase.Syllables, GameManager.VisualTime, ref _builder);

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
                AddToRenderState(phrase.Syllables, GameManager.VisualTime, ref hash);
            }

            return hash.ToHashCode();
        }

        public void Reset()
        {
            _heldPhrases.Clear();
            _lastRenderState = int.MinValue;
            _builder.Clear();
            _phraseText.text = string.Empty;
        }
    }
}