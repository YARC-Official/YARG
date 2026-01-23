using System;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Settings;

namespace YARG.Gameplay.Visuals
{
    public class PhraseLineElement : VocalElement
    {
        public VocalsPhrase PhraseRef;

        private float STATIC_1L_SCALE_Z = 0.85f;
        private float STATIC_1L_POSITION_Z = 0.13f;

        private float STATIC_2L_SCALE_Z = 0.7f;
        private float STATIC_2L_POSITION_Z = 0f;

        private float STATIC_3L_SCALE_Z = 0.575f;
        private float STATIC_3L_POSITION_Z = -0.125f;

        private float? _scaleZ = null;
        private float? _positionZ = null;


        public override double ElementTime => PhraseRef.TimeEnd;

        protected override void InitializeElement()
        {
            if (SettingsManager.Settings.StaticVocalsMode.Value)
            {
                if (_scaleZ is null || _positionZ is null)
                {
                    (_scaleZ, _positionZ) = VocalTrack.LyricLaneCount switch
                    {
                        1 => (STATIC_1L_SCALE_Z, STATIC_1L_POSITION_Z),
                        2 => (STATIC_2L_SCALE_Z, STATIC_2L_POSITION_Z),
                        3 => (STATIC_3L_SCALE_Z, STATIC_3L_POSITION_Z),
                        _ => throw new InvalidOperationException("Unexpected lyric lane count")
                    };
                }

                transform.localPosition = transform.localPosition.WithZ(_positionZ.Value);
                transform.localScale = transform.localScale.WithZ(_scaleZ.Value);
            }
        }

        protected override void UpdateElement()
        {
        }

        protected override void HideElement()
        {
        }
    }
}