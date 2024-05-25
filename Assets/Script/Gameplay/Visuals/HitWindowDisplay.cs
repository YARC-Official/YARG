using System;
using UnityEngine;
using YARG.Core.Logging;
using YARG.Gameplay.Player;
using YARG.Helpers.Extensions;
using YARG.Settings;

namespace YARG.Gameplay.Visuals
{
    public class HitWindowDisplay : MonoBehaviour
    {
        private Transform   _transformCache;
        private TrackPlayer _player;

        private double _lastTotalWindow;
        private double _lastNoteSpeed;
        private double _lastSongSpeed;

        private void Awake()
        {
            _player = GetComponentInParent<TrackPlayer>();

            _transformCache = transform;
            if (!SettingsManager.Settings.ShowHitWindow.Value)
            {
                gameObject.SetActive(false);
            }
        }

        private void Start()
        {
            if (_player is null)
            {
                return;
            }

            SetHitWindowSize();

            // Set fade (required in case the hit window goes past the fade threshold)
            GetComponent<MeshRenderer>().material
                .SetFade(_player.ZeroFadePosition, _player.FadeSize);
        }

        public void SetHitWindowSize()
        {
            var window = _player.BaseEngine.CalculateHitWindow();

            var totalWindow = -window.FrontEnd + window.BackEnd;

            // Only update the hit window if it changed
            if (Math.Abs(totalWindow - _lastTotalWindow) < double.Epsilon &&
                Math.Abs(_player.NoteSpeed - _lastNoteSpeed) < double.Epsilon &&
                Math.Abs(_player.BaseEngine.BaseParameters.SongSpeed - _lastSongSpeed) < double.Epsilon)
            {
                return;
            }

            YargLogger.LogFormatDebug("Updating window to {0}ms at {1}%", Math.Floor(totalWindow * 1000), _player.BaseEngine.BaseParameters.SongSpeed * 100);

            _lastTotalWindow = totalWindow;
            _lastNoteSpeed = _player.NoteSpeed;
            _lastSongSpeed = _player.BaseEngine.BaseParameters.SongSpeed;

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
            if (_player is null)
            {
                return;
            }

            SetHitWindowSize();
        }
    }
}