using System;
using UnityEngine;
using YARG.Gameplay.Player;
using YARG.Settings;

namespace YARG.Gameplay.Visuals
{
    public class HitWindowDisplay : MonoBehaviour
    {
        private Transform  _transformCache;
        private BasePlayer _player;

        private double _hitWindowSize;

        private double _lastNoteSpeed;

        private void Awake()
        {
            _player = GetComponentInParent<BasePlayer>();

            _transformCache = transform;
            if (!SettingsManager.Settings.ShowHitWindow.Data)
            {
                gameObject.SetActive(false);
            }
        }

        private void Start()
        {
            if(_player is null)
            {
                return;
            }

            SetHitWindowSize();
        }

        public void SetHitWindowSize()
        {
            var window = _player.BaseEngine.CalculateHitWindow();

            var totalWindow = -window.FrontEnd + window.BackEnd;
            if (Math.Abs(totalWindow - _hitWindowSize) < double.Epsilon && Math.Abs(_player.NoteSpeed - _lastNoteSpeed) < double.Epsilon)
            {
                return;
            }

            _hitWindowSize = totalWindow;
            _lastNoteSpeed = _player.NoteSpeed;

            // Offsetting is done based on half of the size
            float baseOffset = (float) (-window.FrontEnd - window.BackEnd) / 2f;

            _transformCache.localScale = _transformCache.localScale
                .WithY((float) totalWindow * _player.NoteSpeed);
            _transformCache.localPosition = _transformCache.localPosition
                .WithZ(baseOffset);
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