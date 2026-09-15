using System;
using TMPro;
using UnityEngine;
using YARG.Core.Chart;

namespace YARG.Gameplay.Visuals
{
    public class VocalScrollingLyricSyllableElement : VocalElement
    {
        public sealed class PreparedLyric
        {
            public readonly LyricEvent Lyric;
            public readonly string DisplayText;
            public readonly FontStyles FontStyle;
            public readonly float Width;
            public readonly bool IsHidden;

            public PreparedLyric(LyricEvent lyric, bool allowHiding, float width)
            {
                Lyric = lyric;
                DisplayText = lyric.HarmonyHidden && allowHiding ? string.Empty : lyric.Text;
                FontStyle = lyric.NonPitched ? FontStyles.Italic : FontStyles.Normal;
                IsHidden = string.IsNullOrEmpty(DisplayText);
                Width = IsHidden ? 0f : width;
            }
        }

        private LyricEvent _lyricRef;
        private PreparedLyric _preparedLyric;

        private double _minimumTime;
        private bool _isStarpower;

        private int _harmonyIndex;
        private bool _allowHiding;

        private readonly Color _highlightColor = new Color(0.0549f, 0.6431f, 0.9765f);
        private readonly Color _pastColor      = new Color(0.349f, 0.349f, 0.349f);

        public override double ElementTime => Math.Max(_lyricRef.Time, _minimumTime);

        [SerializeField]
        private TextMeshPro _lyricText;

        public float Width => _preparedLyric.Width;

        public void Initialize(PreparedLyric preparedLyric, double minTime,
            bool isStarpower, int harmonyIndex, bool allowHiding)
        {
            _preparedLyric = preparedLyric;
            _lyricRef = preparedLyric.Lyric;

            _minimumTime = minTime;
            _isStarpower = isStarpower;

            _harmonyIndex = harmonyIndex;
            _allowHiding = allowHiding;
        }

        protected override void InitializeElement()
        {
            _lyricText.text = _preparedLyric.DisplayText;
            _lyricText.fontStyle = _preparedLyric.FontStyle;

            // Disable automatically if the text is just nothing
            if (string.IsNullOrEmpty(_lyricText.text))
            {
                ParentPool.Return(this);
            }
        }

        protected override void UpdateElement()
        {
            if (GameManager.VisualTime < _lyricRef.Time)
            {
                _lyricText.color = _isStarpower ? Color.yellow : Color.white;
            }
            else if (GameManager.VisualTime > _lyricRef.Time && GameManager.VisualTime < _lyricRef.TimeEnd)
            {
                _lyricText.color = _highlightColor;
            }
            else
            {
                _lyricText.color = _pastColor;
            }
        }

        protected override void HideElement()
        {
        }
    }
}
