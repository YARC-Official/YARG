using System;
using TMPro;
using UnityEngine;
using YARG.Core.Chart;

namespace YARG.Gameplay.Visuals
{
    public class VocalScrollingLyricSyllableElement : VocalElement
    {
        private LyricEvent _lyricRef;
        private double _lyricLength;

        private double _minimumTime;
        private bool _isStarpower;

        private int _harmonyIndex;
        private bool _allowHiding;

        public override double ElementTime => Math.Max(_lyricRef.Time, _minimumTime);

        [SerializeField]
        private TextMeshPro _lyricText;

        public float Width => _lyricText.GetPreferredValues().x;

        public void Initialize(LyricEvent lyric, double minTime, double lyricLength,
            bool isStarpower, int harmonyIndex, bool allowHiding)
        {
            _lyricRef = lyric;
            _lyricLength = lyricLength;

            _minimumTime = minTime;
            _isStarpower = isStarpower;

            _harmonyIndex = harmonyIndex;
            _allowHiding = allowHiding;
        }

        protected override void InitializeElement()
        {
            if (_lyricRef.HarmonyHidden && _allowHiding)
            {
                _lyricText.text = string.Empty;
            }
            else
            {
                _lyricText.text = _lyricRef.Text;
            }

            // If it's a talkie, italicize it
            _lyricText.fontStyle = _lyricRef.NonPitched ? FontStyles.Italic : FontStyles.Normal;

            // Disable automatically if the text is just nothing
            if (string.IsNullOrEmpty(_lyricText.text))
            {
                ParentPool.Return(this);
            }
        }

        protected override void UpdateElement()
        {
            if (GameManager.SongTime < _lyricRef.Time)
            {
                _lyricText.color = _isStarpower ? Color.yellow : Color.white;
            }
            else if (GameManager.SongTime > _lyricRef.Time && GameManager.SongTime < _lyricRef.Time + _lyricLength)
            {
                _lyricText.color = new Color(0.0549f, 0.6431f, 0.9765f);
            }
            else
            {
                _lyricText.color = new Color(0.349f, 0.349f, 0.349f);
            }
        }

        protected override void HideElement()
        {
        }
    }
}