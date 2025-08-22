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

        private Utf16ValueStringBuilder _phraseBuilder;
        private Utf16ValueStringBuilder _syllableBuilder;

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

            _phraseBuilder = ZString.CreateStringBuilder(false);
            _syllableBuilder = ZString.CreateStringBuilder(false);
        }

        protected override void InitializeElement()
        {
            if (_isFuture)
            {
                _phraseBuilder.Append(FUTURE_PHRASE_COLOR_TAG);

                for (var lyricIdx = 0; lyricIdx < _phraseRef.Lyrics.Count; lyricIdx++)
                {
                    var lyric = _phraseRef.Lyrics[lyricIdx];
                    var lastLyricOfPhrase = lyricIdx == _phraseRef.Lyrics.Count - 1;
                    _phraseBuilder.Append(ConstructStaticSyllable(lyric, lastLyricOfPhrase));
                }

                _phraseBuilder.Append(CLOSE_COLOR_TAG);
            }
            else
            {
                _phraseBuilder.Append(FUTURE_LYRIC_COLOR_TAG);

                for (var lyricIdx = 0; lyricIdx < _phraseRef.Lyrics.Count; lyricIdx++)
                {
                    var lyric = _phraseRef.Lyrics[lyricIdx];
                    var lastLyricOfPhrase = lyricIdx == _phraseRef.Lyrics.Count - 1;
                    _phraseBuilder.Append(ConstructStaticSyllable(lyric, lastLyricOfPhrase));
                }

                _phraseBuilder.Append(CLOSE_COLOR_TAG);
            }

            transform.localPosition = transform.localPosition.WithX(_x);

            _phraseText.text = _phraseBuilder.ToString();
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

            _phraseBuilder.Clear();

            for (var lyricIdx = 0; lyricIdx < _phraseRef.Lyrics.Count; lyricIdx++)
            {
                var lyric = _phraseRef.Lyrics[lyricIdx];
                var lastLyricOfPhrase = lyricIdx == _phraseRef.Lyrics.Count - 1;

                var probableLyricEnd = GetProbableNoteEndOfLyric(_phraseRef, lyric);

                if (probableLyricEnd <= GameManager.SongTime)
                {
                    _phraseBuilder.Append($"{PAST_LYRIC_COLOR_TAG}{ConstructStaticSyllable(lyric, lastLyricOfPhrase)}{CLOSE_COLOR_TAG}");
                }

                else if (lyric.Time <= GameManager.SongTime && GameManager.SongTime < probableLyricEnd)
                {
                    _phraseBuilder.Append($"{PRESENT_LYRIC_COLOR_TAG}{ConstructStaticSyllable(lyric, lastLyricOfPhrase)}{CLOSE_COLOR_TAG}");
                }

                else
                {
                    _phraseBuilder.Append($"{FUTURE_LYRIC_COLOR_TAG}{ConstructStaticSyllable(lyric, lastLyricOfPhrase)}{CLOSE_COLOR_TAG}");
                }
            }

            _phraseText.text = _phraseBuilder.ToString();
        }

        protected override bool UpdateElementPosition()
        {
            return true;
        }

        protected override void HideElement()
        {
        }

        private string ConstructStaticSyllable(LyricEvent lyric, bool lastLyricOfPhrase)
        {
            _syllableBuilder.Clear();

            if (lyric.NonPitched)
            {
                _syllableBuilder.Append("<i>");
            }

            if (lyric.JoinWithNext)
            {
                _syllableBuilder.Append(lyric.Text[0..^1]);
            }
            else
            {
                _syllableBuilder.Append(lyric.Text);
                if (!lyric.HyphenateWithNext && !lastLyricOfPhrase)
                {
                    _syllableBuilder.Append(" ");
                }
            }

            if (lyric.NonPitched)
            {
                _syllableBuilder.Append("</i>");
            }

            return _syllableBuilder.ToString();
        }

        private static double GetProbableNoteEndOfLyric(VocalsPhrase phrase, LyricEvent lyric)
        {
            return phrase.PhraseParentNote.ChildNotes
                .FirstOrDefault(note => note.Tick == lyric.Tick).TotalTimeEnd;
        }
    }
}
