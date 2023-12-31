using System;
using TMPro;
using UnityEngine;
using YARG.Core.Chart;

namespace YARG.Gameplay.Visuals
{
    public class VocalLyricElement : VocalElement
    {
        private LyricEvent _lyricRef;
        private double _lyricLength;

        private double _minimumTime;
        private bool _isStarpower;
        private bool _isTalkie;

        private int _harmonyIndex;

        public override double ElementTime => Math.Max(_lyricRef.Time, _minimumTime);

        [SerializeField]
        private TextMeshPro _lyricText;

        public float Width => _lyricText.GetPreferredValues().x;

        public void Initialize(LyricEvent lyric, double minTime, double lyricLength,
            bool isStarpower, bool isTalkie, int harmonyIndex)
        {
            _lyricRef = lyric;
            _lyricLength = lyricLength;

            _minimumTime = minTime;
            _isStarpower = isStarpower;
            _isTalkie = isTalkie;

            _harmonyIndex = harmonyIndex;
        }

        protected override void InitializeElement()
        {
            // TODO: This check doesn't actually work currently
            // Need to add more flag handling to YARG.Core first
            if (_lyricRef.Text.StartsWith('$') && _harmonyIndex != 0)
                _lyricText.text = string.Empty;
            else
                _lyricText.text = _lyricRef.Text;

            // If it's a talkie, italicize it
            _lyricText.fontStyle = _isTalkie ? FontStyles.Italic : FontStyles.Normal;

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