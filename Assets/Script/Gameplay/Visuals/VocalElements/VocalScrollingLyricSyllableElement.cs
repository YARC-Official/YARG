using System;
using TMPro;
using UnityEngine;
using YARG.Core.Chart;

namespace YARG.Gameplay.Visuals
{
    public class VocalScrollingLyricSyllableElement : VocalElement
    {
        private static readonly int _sharpness = Shader.PropertyToID("_Sharpness");
        private const float VOCAL_LYRIC_SHARPNESS = 0.5f;

        private LyricEvent _lyricRef;
        private double _lyricLength;
        private bool _sharpnessApplied;

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
            ApplySharpness();

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
            if (GameManager.VisualTime < _lyricRef.Time)
            {
                _lyricText.color = _isStarpower ? Color.yellow : Color.white;
            }
            else if (GameManager.VisualTime > _lyricRef.Time && GameManager.VisualTime < _lyricRef.Time + _lyricLength)
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

        private void ApplySharpness()
        {
            if (_sharpnessApplied)
            {
                return;
            }

            var material = _lyricText.fontMaterial;
            if (material != null && material.HasFloat(_sharpness))
            {
                material.SetFloat(_sharpness, VOCAL_LYRIC_SHARPNESS);
            }

            _sharpnessApplied = true;
        }
    }
}