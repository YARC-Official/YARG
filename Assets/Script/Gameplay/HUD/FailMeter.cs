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
        private RectTransform _sliderContainer;

        private Slider[] _playerSliders;
        private Tweener[] _happinessTweeners;
        private Tweener[] _xposTweeners;
        private Tweener _meterRedTweener;
        private Tweener _meterGreenTweener;
        private Tweener _bandFillTweener;
        private float[] _previousPlayerHappiness;
        private float _previousBandHappiness;

        private Vector2[] _playerPositions;
        private Vector2[] _xPosVectors;

        private float _containerHeight;


        // TODO: Should probably make a more specific class we can reference here
        private EngineManager _engineManager;
        private GameManager _gameManager;

        private List<EngineManager.EngineContainer> _players = new();

        private const float SPRITE_SIZE = 35;
        // Allows some overlap
        private const float SPRITE_OFFSET = SPRITE_SIZE * 0.8f;

        // GameManager will have to initialize us
        public void Initialize(EngineManager engineManager, GameManager gameManager)
        {
            _gameManager = gameManager;
            _engineManager = engineManager;
            _players.AddRange(engineManager.Engines);

            _playerSliders = new Slider[_players.Count];
            _xposTweeners = new Tweener[_players.Count];
            _happinessTweeners = new Tweener[_players.Count];
            _previousPlayerHappiness = new float[_players.Count];
            _playerPositions = new Vector2[_players.Count];
            _xPosVectors = new Vector2[_players.Count];
            _containerHeight = _sliderContainer.rect.height;

            // Cache tweens for later use
            _meterRedTweener = _fillImage.DOColor(Color.red, 0.25f).
                SetLoops(-1, LoopType.Yoyo).
                SetEase(Ease.InOutSine).
                SetAutoKill(false).Pause();

            _meterGreenTweener = _fillImage.DOColor(Color.green, 0.25f).
                SetAutoKill(false).
                Pause();
            // 0.8f is an arbitrary placeholder
            _bandFillTweener = _fillImage.DOFillAmount(0.8f, 0.125f).
                SetAutoKill(false);




            // attach the slider instances to the scene and apply the correct icon
            for (int i = 0; i < _players.Count; i++)
            {
                _playerSliders[i] = Instantiate(_sliderPrefab, _sliderContainer);
                // y value is ignored, so it is ok that it is zero here
                _xPosVectors[i] = new Vector2(SPRITE_OFFSET * (i + 1) + SPRITE_OFFSET * 0.2f, 0);

                _xposTweeners[i] = _playerSliders[i].handleRect.DOAnchorPosX(_xPosVectors[i].x, 0.125f).SetAutoKill(false);
                _playerPositions[i] = _playerSliders[i].handleRect.transform.position;

                var handleImage = _playerSliders[i].handleRect.GetComponentInChildren<Image>();
                var spriteName = $"InstrumentIcons[{_players[i].Instrument.ToResourceName()}]";
                var sprite = Addressables.LoadAssetAsync<Sprite>(spriteName).WaitForCompletion();
                handleImage.sprite = sprite;
                _playerSliders[i].value = 0.01f;
                _playerSliders[i].gameObject.SetActive(true);

                // Cached for reuse because starting a new tween generates garbage
                _happinessTweeners[i] = _playerSliders[i].DOValue(_players[i].Happiness, 0.5f).SetAutoKill(false);
                _previousPlayerHappiness[i] = _players[i].Happiness;
            }

            YargLogger.LogDebug("Initialized fail meter");
        }

        // Update is called once per frame
        private void Update()
        {
            // Don't crash the whole game if we didn't get initialized and still manage to somehow become active
            if (_engineManager == null)
            {
                YargLogger.LogDebug("FailMeter not initialized");
                return;
            }

            // No need for any of this if we're paused anyway
            if (_gameManager.Paused)
            {
                return;
            }

            if (_previousBandHappiness != _engineManager.Happiness)
            {
                // TODO: Cache and reuse these tweens to avoid generating unnecessary garbage
                var happiness = _engineManager.Happiness;

                // We only want to do this once when passing the threshold
                if (happiness < 0.33f && _previousBandHappiness >= 0.33f)
                {
                    if (_meterGreenTweener.active)
                    {
                        _meterGreenTweener.Pause();
                    }
                    _fillImage.color = Color.black;
                    _meterRedTweener.Restart();
                }
                else if (happiness >= 0.33f && _previousBandHappiness < 0.33f)
                {
                    if (_meterRedTweener.active)
                    {
                        _meterRedTweener.Pause();
                    }
                    _meterGreenTweener.Restart();
                }

                _bandFillTweener.ChangeValues(_fillImage.fillAmount, happiness).Play();

                _previousBandHappiness = _engineManager.Happiness;

            }

            for (var i = 0; i < _players.Count; i++)
            {
                // Convert happiness to a new y value
                float newY = (_containerHeight * _players[i].Happiness) + (_sliderContainer.position.y - (_containerHeight / 2));
                int overlap = 0;
                // Check if we will overlap another icon
                for (var j = i; j < _players.Count; j++)
                {
                    if (j == i)
                    {
                        // Ignore self
                        continue;
                    }

                    if (newY >= _playerPositions[j].y - SPRITE_OFFSET &&
                        newY <= _playerPositions[j].y + SPRITE_OFFSET)
                    {
                        overlap++;
                    }
                }

                // The extra SPRITE_OFFSET * 0.2f is to get the whole group a bit farther from the meter itself
                _xPosVectors[i].x = SPRITE_OFFSET * (overlap + 1) + SPRITE_OFFSET * 0.2f;

                _xposTweeners[i].ChangeEndValue(_xPosVectors[i], 0.125f, true).Play();

                // We only need the x, y so it's fine that we're converting Vector3 to Vector2 here
                _playerPositions[i] = _playerSliders[i].handleRect.transform.position;

                // This we can not do if the current player's happiness hasn't changed
                if (_previousPlayerHappiness[i] != _players[i].Happiness)
                {
                    _happinessTweeners[i].ChangeValues(_playerSliders[i].value, _players[i].Happiness, 0.1f);
                    // Not sure if strictly necessary, but it seems like good practice to not try to play a playing tween
                    if (_happinessTweeners[i].IsComplete())
                    {
                        _happinessTweeners[i].Play();
                    }
                    else
                    {
                        _happinessTweeners[i].Restart();
                    }
                }

                _previousPlayerHappiness[i] = _players[i].Happiness;

            }
        }

        private void OnDisable()
        {
            // Make sure the tweens are dead
            _meterRedTweener?.Kill();
            _meterGreenTweener?.Kill();
            _bandFillTweener?.Kill();
            foreach (var tween in _happinessTweeners)
            {
                tween.Kill();
            }

            foreach (var tween in _xposTweeners)
            {
                tween.Kill();
            }
        }
    }
}
