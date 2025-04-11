using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Rendering.VirtualTexturing;
using UnityEngine.Serialization;
using UnityEngine.UI;
using YARG.Core.Engine;
using YARG.Core.Logging;
using YARG.Helpers.Extensions;

namespace YARG.Gameplay.HUD
{
    public class FailMeter : MonoBehaviour
    {
        [FormerlySerializedAs("Slider")]
        [SerializeField]
        private Slider _bandSlider;
        [FormerlySerializedAs("FillImage")]
        [SerializeField]
        private Image _fillImage;
        [SerializeField]
        private Slider _sliderPrefab;
        [SerializeField]
        private Transform _sliderContainer;

        private Slider[] _playerSliders;
        private Tweener[] _playerTweeners;
        private float[] _previousPlayerHappiness;


        // TODO: Should probably make a more specific class we can reference here
        private EngineManager _manager;

        private List<EngineManager.EngineContainer> _players = new();

        private const int SPRITE_OFFSET = -65;


        // GameManager will have to initialize us
        public void Initialize(EngineManager manager)
        {
            _manager = manager;
            _players.AddRange(manager.Engines);

            _playerSliders = new Slider[_players.Count];
            _playerTweeners = new Tweener[_players.Count];
            _previousPlayerHappiness = new float[_players.Count];
            // We have to somehow attach the slider instances to the scene and apply the correct icon
            for (int i = 0; i < _players.Count; i++)
            {
                float offset = (float) (SPRITE_OFFSET * (Math.Floor(i / (float) 2) + 1));
                _playerSliders[i] = Instantiate(_sliderPrefab, _sliderContainer);
                // TODO: Fix this, it looks like shit
                if (i % 2 == 1)
                {
                    _playerSliders[i].handleRect.offsetMin =
                        new Vector2(offset, _playerSliders[i].handleRect.offsetMin.y);
                }
                else
                {
                    _playerSliders[i].handleRect.offsetMax =
                        new Vector2(offset * -1, _playerSliders[i].handleRect.offsetMax.y);
                }

                var handleImage = _playerSliders[i].handleRect.GetComponentInChildren<Image>();
                var spriteName = $"InstrumentIcons[{_players[i].Instrument.ToResourceName()}]";
                var sprite = Addressables.LoadAssetAsync<Sprite>(spriteName).WaitForCompletion();
                handleImage.sprite = sprite;
                _playerSliders[i].value = 0.01f;
                _playerSliders[i].gameObject.SetActive(true);
                // Cached for reuse because starting a new tween generates garbage
                _playerTweeners[i] = _playerSliders[i].DOValue(_players[i].Happiness, 0.5f).SetAutoKill(false);
                _previousPlayerHappiness[i] = _players[i].Happiness;
            }

            YargLogger.LogDebug("Initialized fail meter");
        }

        // Update is called once per frame
        private void Update()
        {
            // Don't crash the whole game if we didn't get initialized for some reason
            if (_manager == null)
            {
                YargLogger.LogDebug("FailMeter not initialized");
                return;
            }

            var happiness = _manager.Happiness;
            if (happiness < 0.33f)
            {
                _fillImage.color = Color.red;
            }
            else
            {
                _fillImage.color = Color.green;
            }

            _fillImage.fillAmount = happiness;

            for (var i = 0; i < _players.Count; i++)
            {
                if (_previousPlayerHappiness[i] != _players[i].Happiness)
                {
                    _playerTweeners[i].ChangeValues(_playerSliders[i].value, _players[i].Happiness, 0.1f);
                    // Not sure if strictly necessary, but it seems like good practice to not try to play a playing tween
                    if (_playerTweeners[i].IsComplete())
                    {
                        _playerTweeners[i].Play();
                    }
                    else
                    {
                        _playerTweeners[i].Restart();
                    }
                }
            }
        }
    }
}
