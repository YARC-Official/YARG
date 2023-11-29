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
            SetHitWindowSize();
        }

        private void SetHitWindowSize()
        {
            var window = _player.BaseEngine.CalculateHitWindow();

            var totalWindow = -window.FrontEnd + window.BackEnd;
            if (Math.Abs(totalWindow - _hitWindowSize) < double.Epsilon)
            {
                return;
            }

            _hitWindowSize = totalWindow;

            // Offsetting is done based on half of the size
            float baseOffset = (float) (-window.FrontEnd - window.BackEnd) / 2f;

            _transformCache.localScale = _transformCache.localScale
                .WithY((float) totalWindow * _player.NoteSpeed);
            _transformCache.localPosition = _transformCache.localPosition
                .WithZ(baseOffset);
        }

        private void Update()
        {
            if (!_player.HitWindow.IsDynamic)
            {
                return;
            }

            SetHitWindowSize();
        }
    }
}