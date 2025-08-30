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

        // Time from the beginning of the first note to the end of the last (0 if there are no notes)
        // If this is shorter than the lyric shift duration, we'll shift in this amount of time to make sure we don't get caught in the middle of a shift
        public float Duration => _phraseRef.PhraseParentNote.ChildNotes.Count == 0 ?
            0f : (float)(_phraseRef.PhraseParentNote.ChildNotes[^1].TimeEnd - _phraseRef.PhraseParentNote.ChildNotes[0].Time);

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
            }
            else
            {
                _builder.Append(FUTURE_LYRIC_COLOR_TAG);
            }

            for (var lyricIdx = 0; lyricIdx < _phraseRef.Lyrics.Count; lyricIdx++)
            {
                var lyric = _phraseRef.Lyrics[lyricIdx];
                var lastLyricOfPhrase = lyricIdx == _phraseRef.Lyrics.Count - 1;
                ConstructStaticSyllable(lyric, null, lastLyricOfPhrase);
            }

            _builder.Append(CLOSE_COLOR_TAG);

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

            for (var lyricIdx = 0; lyricIdx < _phraseRef.Lyrics.Count; lyricIdx++)
            {
                var lyric = _phraseRef.Lyrics[lyricIdx];
                var lastLyricOfPhrase = lyricIdx == _phraseRef.Lyrics.Count - 1;

                var probableLyricEnd = GetProbableNoteEndOfLyric(_phraseRef, lyric);

                if (probableLyricEnd <= GameManager.SongTime)
                {
                    ConstructStaticSyllable(lyric, PAST_LYRIC_COLOR_TAG, lastLyricOfPhrase);
                }

                else if (lyric.Time <= GameManager.SongTime && GameManager.SongTime < probableLyricEnd)
                {
                    ConstructStaticSyllable(lyric, PRESENT_LYRIC_COLOR_TAG, lastLyricOfPhrase);
                }

                else
                {
                    ConstructStaticSyllable(lyric, FUTURE_LYRIC_COLOR_TAG, lastLyricOfPhrase);
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

        private void ConstructStaticSyllable(LyricEvent lyric, string? colorTag, bool lastLyricOfPhrase)
        {
            if (_harmonyIndex is not 0 && lyric.HarmonyHidden && _allowHiding)
            {
                return;
            }

            if (colorTag is not null)
            {
                _builder.Append(colorTag);
            }

            if (lyric.NonPitched)
            {
                _builder.Append("<i>");
            }

            if (lyric.JoinWithNext)
            {
                _builder.Append(lyric.Text[0..^1]);
                if (lyric.Text[^1] is '=')
                {
                    _builder.Append("-");
                }
            } else
            {
                _builder.Append(lyric.Text);
                _builder.Append(" ");
            }

            if (lyric.NonPitched)
            {
                _builder.Append("</i>");
            }

            if (!lyric.JoinWithNext && !lastLyricOfPhrase)
            {
                _builder.Append(" ");
            }

            if (colorTag is not null)
            {
                _builder.Append(CLOSE_COLOR_TAG);
            }
        }

        private static double GetProbableNoteEndOfLyric(VocalsPhrase phrase, LyricEvent lyric)
        {
            return phrase.PhraseParentNote.ChildNotes
                .FirstOrDefault(note => note.Tick == lyric.Tick).TotalTimeEnd;
        }
    }
}
