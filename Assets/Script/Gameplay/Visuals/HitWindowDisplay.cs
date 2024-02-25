using System;
using UnityEngine;
using YARG.Gameplay.Player;
using YARG.Settings;

namespace YARG.Gameplay.Visuals
{
    public class HitWindowDisplay : GameplayBehaviour
    {
        private Transform  _transformCache;
        private BasePlayer _player;

        private double _lastTotalWindow;

        private double _lastNoteSpeed;

        protected override void GameplayAwake()
        {
            _player = GetComponentInParent<BasePlayer>();

            _transformCache = transform;
            if (!SettingsManager.Settings.ShowHitWindow.Value)
            {
                gameObject.SetActive(false);
            }
        }

        private void Start()
        {
            if (_player is null) return;

            SetHitWindowSize();
        }

        public void SetHitWindowSize()
        {
            var window = _player.BaseEngine.CalculateHitWindow();

            var totalWindow = -window.FrontEnd + window.BackEnd;

            // Only update the hit window if it changed
            if (Math.Abs(totalWindow - _lastTotalWindow) < double.Epsilon
                && Math.Abs(_player.NoteSpeed - _lastNoteSpeed) < double.Epsilon)
            {
                return;
            }

            _lastTotalWindow = totalWindow;
            _lastNoteSpeed = _player.NoteSpeed;

            // Offsetting is done based on half of the size
            float baseOffset = ((float) (-window.FrontEnd - window.BackEnd) / 2f);

            // Offset via calibration
            float videoCalibrationOffset = -SettingsManager.Settings.VideoCalibration.Value / 1000f;
            float inputCalibrationOffset = (float) -_player.Player.Profile.InputCalibrationSeconds;

            _transformCache.localScale = _transformCache.localScale
                .WithY((float) totalWindow * _player.NoteSpeed);
            _transformCache.localPosition = _transformCache.localPosition
                .WithZ((baseOffset + videoCalibrationOffset + inputCalibrationOffset) * _player.NoteSpeed);
        }

        private void Update()
        {
            // Player could be null if the hit window display is being used in customisation menu
            if (_player is null || !_player.HitWindow.IsDynamic)
            {
                return;
            }

            SetHitWindowSize();
        }
    }
}