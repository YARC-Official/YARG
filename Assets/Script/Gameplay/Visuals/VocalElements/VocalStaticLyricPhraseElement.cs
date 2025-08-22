using Cysharp.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Gameplay.Player;
using YARG.Gameplay.Visuals;

namespace YARG.Gameplay.Visuals
{
    public class VocalStaticLyricPhraseElement : BaseElement // not a VocalElement because it doesn't scroll along the highway
    {
        private const string PAST_LYRIC_COLOR_TAG = "<color=#595959>";
        private const string PRESENT_LYRIC_COLOR_TAG = "<color=#5CB9FF>";
        private const string FUTURE_LYRIC_COLOR_TAG = "<color=#FFFFFF>";
        private const string FUTURE_PHRASE_COLOR_TAG = "<color=#595959>";
        private const string CLOSE_COLOR_TAG = "</color>";

        private VocalsPhrase _phraseRef;
        private VocalStaticLyricPhraseElement? _previousPhraseElement;
        private bool _isStarpower;
        private int _harmonyIndex;
        private bool _allowHiding;
        private float _x;
        private bool _isFuture = true;

        private Utf16ValueStringBuilder _builder;

        public override double ElementTime => _phraseRef.Time;

        [SerializeField]
        private TextMeshPro _phraseText;

        public float Width => _phraseText.GetPreferredValues().x;

        public void Initialize(VocalsPhrase phrase, bool isStarpower, int harmonyIndex,
            bool allowHiding, float x)
        {
            _phraseRef = phrase;
            _isStarpower = isStarpower;
            _harmonyIndex = harmonyIndex;
            _allowHiding = allowHiding;
            _x = x;

            _builder = ZString.CreateStringBuilder(false);
        }

        protected override void InitializeElement()
        {
            if (_isFuture)
            {
                _builder.Append(FUTURE_PHRASE_COLOR_TAG);

                foreach (var lyric in _phraseRef.Lyrics)
                {
                    _builder.Append(RenderStaticSyllable(lyric));
                }

                _builder.Append(CLOSE_COLOR_TAG);
            }
            else
            {
                _builder.Append(FUTURE_LYRIC_COLOR_TAG);

                foreach (var lyric in _phraseRef.Lyrics)
                {
                    _builder.Append(RenderStaticSyllable(lyric));
                }

                _builder.Append(CLOSE_COLOR_TAG);
            }

            transform.localPosition = transform.localPosition.WithX(_x);

            _phraseText.text = _builder.ToString();
        }

        public void Activate()
        {
            _isFuture = false;
        }

        public void Dismiss()
        {
            _isFuture = true;
            DisableIntoPool();
            ParentPool.Return(this);
        }

        protected override void UpdateElement()
        {
            if (_isFuture)
            {
                // Future phrases don't need to update after initialization, until they become the active phrase
                return;
            }

            _builder.Clear();

            foreach (var lyric in _phraseRef.Lyrics)
            {
                var probableLyricEnd = GetProbableNoteEndOfLyric(_phraseRef, lyric);

                if (probableLyricEnd <= GameManager.SongTime)
                {
                    _builder.Append($"{PAST_LYRIC_COLOR_TAG}{RenderStaticSyllable(lyric)}{CLOSE_COLOR_TAG}");
                }

                else if (lyric.Time <= GameManager.SongTime && GameManager.SongTime < probableLyricEnd)
                {
                    _builder.Append($"{PRESENT_LYRIC_COLOR_TAG}{RenderStaticSyllable(lyric)}{CLOSE_COLOR_TAG}");
                }

                else
                {
                    _builder.Append($"{FUTURE_LYRIC_COLOR_TAG}{RenderStaticSyllable(lyric)}{CLOSE_COLOR_TAG}");
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

        private static string RenderStaticSyllable(LyricEvent lyric)
        {
            string text;

            if (lyric.JoinWithNext)
            {
                text = lyric.Text[0..^1];
            }
            else
            {
                text = $"{lyric.Text} ";
            }

            if (lyric.NonPitched)
            {
                text = $"<i>{text}</i>";
            }

            return text;
        }

        private static double GetProbableNoteEndOfLyric(VocalsPhrase phrase, LyricEvent lyric)
        {
            return phrase.PhraseParentNote.ChildNotes
                .FirstOrDefault(note => note.Tick == lyric.Tick).TotalTimeEnd;
        }
    }
}
