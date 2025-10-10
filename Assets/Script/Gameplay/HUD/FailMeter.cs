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
using YARG.Settings;

namespace YARG.Gameplay.HUD
{
    public class FailMeter : MonoBehaviour
    {
        [SerializeField]
        private GameObject _meterContainer;
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

        private Slider[]  _playerSliders;
        private Tweener[] _happinessTweeners = Array.Empty<Tweener>();
        private Tweener[] _xposTweeners      = Array.Empty<Tweener>();
        private Tweener   _meterRedTweener;
        private Tweener   _meterYellowTweener;
        private Tweener   _meterGreenTweener;
        private Tweener   _bandFillTweener;
        private Tweener   _meterPositionTweener;
        private float[]   _previousPlayerHappiness;
        private float     _previousBandHappiness;

        private MeterColor _previousMeterColor;

        private Vector2[] _playerPositions;
        private Vector2[] _xPosVectors;

        private Vector3 _initialPosition;

        private float _containerHeight;

        private bool _intendedActive;


        // TODO: Should probably make a more specific class we can reference here
        private EngineManager _engineManager;
        private GameManager _gameManager;

        private List<EngineManager.EngineContainer> _players = new();

        // Allows some overlap
        private const float HAPPINESS_COLLISION_RANGE = 0.06f;
        private const float SPRITE_OVERLAP_OFFSET = 28f;
        private const float SPRITE_INITIAL_OFFSET = 35f;

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

            _initialPosition = _meterContainer.transform.position;

            // Cache tweens for later use
            _meterRedTweener = _fillImage.DOColor(Color.red, 0.25f).
                SetLoops(-1, LoopType.Yoyo).
                SetEase(Ease.InOutSine).
                SetAutoKill(false).Pause();

            _meterYellowTweener = _fillImage.DOColor(Color.yellow, 0.25f).
                SetAutoKill(false).
                Pause();

            _meterGreenTweener = _fillImage.DOColor(Color.green, 0.25f).
                SetAutoKill(false).
                Pause();

            // 0.8f is an arbitrary placeholder
            _bandFillTweener = _fillImage.DOFillAmount(0.8f, 0.125f).
                SetAutoKill(false);

            // This is set up to move the container offscreen, but may later be used to move it back on
            _meterPositionTweener = _meterContainer.transform.DOMoveY(-400f, 0.5f).
                SetAutoKill(false).
                Pause();


            // attach the slider instances to the scene and apply the correct icon
            for (int i = 0; i < _players.Count; i++)
            {
                _playerSliders[i] = Instantiate(_sliderPrefab, _sliderContainer);
                // y value is ignored, so it is ok that it is zero here
                var xOffset = SPRITE_INITIAL_OFFSET + (SPRITE_OVERLAP_OFFSET * i);
                _xPosVectors[i] = new Vector2(xOffset, 0);

                _xposTweeners[i] = _playerSliders[i].handleRect.DOAnchorPosX(_xPosVectors[i].x, 0.125f).SetAutoKill(false);
                _playerPositions[i] = _playerSliders[i].handleRect.transform.position;

                var handleImage = _playerSliders[i].handleRect.GetComponentInChildren<Image>();
                var spriteName = _players[i].GetInstrumentSprite();

                var sprite = Addressables.LoadAssetAsync<Sprite>(spriteName).WaitForCompletion();
                handleImage.sprite = sprite;
                handleImage.color = _players[i].GetHarmonyColor();

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
                return;
            }

            // No need for any of this if we're paused anyway
            if (_gameManager.Paused)
            {
                return;
            }

            if (_previousBandHappiness != _engineManager.Happiness)
            {
                UpdateMeterFill();
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

                    if (Math.Abs(_players[i].Happiness - _players[j].Happiness) < HAPPINESS_COLLISION_RANGE)
                    {
                        overlap++;
                    }
                }

                // The extra SPRITE_INITIAL_OFFSET is to get the whole group a bit farther from the meter itself
                var xOffset =  SPRITE_INITIAL_OFFSET + (SPRITE_OVERLAP_OFFSET * overlap);
                _xPosVectors[i].x = xOffset;

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

        private void UpdateMeterFill()
        {
            var happiness = _engineManager.Happiness;

            var currentColor = GetMeterColor(happiness);
            if (currentColor != _previousMeterColor)
            {
                ApplyColor(currentColor);
                _previousMeterColor = currentColor;
            }

            _bandFillTweener.ChangeValues(_fillImage.fillAmount, happiness).Play();

            _previousBandHappiness = _engineManager.Happiness;
        }

        private void ApplyColor(MeterColor color)
        {
            if (_meterRedTweener.active)
            {
                _meterRedTweener.Pause();
            }

            if (_meterYellowTweener.active)
            {
                _meterYellowTweener.Pause();
            }

            if (_meterGreenTweener.active)
            {
                _meterGreenTweener.Pause();
            }

            switch (color)
            {
                case MeterColor.Red:
                    _fillImage.color = Color.black;
                    _meterRedTweener.Restart();
                    break;
                case MeterColor.Yellow:
                    _meterYellowTweener.Restart();
                    break;
                case MeterColor.Green:
                    _meterGreenTweener.Restart();
                    break;
            }
        }

        public void SetActive(bool active)
        {
            if (active)
            {
                // Move onscreen
                _meterPositionTweener.PlayBackwards();
            }

            if (!active)
            {
                // Move offscreen
                _meterPositionTweener.PlayForward();
            }
        }

        private static MeterColor GetMeterColor(float happiness)
        {
            return happiness switch
            {
                < 0.33f => MeterColor.Red,
                < 0.66f => MeterColor.Yellow,
                _       => MeterColor.Green
            };
        }

        private void OnDisable()
        {
            // Make sure the tweens are dead
            _meterRedTweener?.Kill();
            _meterYellowTweener?.Kill();
            _meterGreenTweener?.Kill();
            _bandFillTweener?.Kill();
            _meterPositionTweener?.Kill();
            foreach (var tween in _happinessTweeners)
            {
                tween.Kill();
            }

            foreach (var tween in _xposTweeners)
            {
                tween.Kill();
            }
        }

        private enum MeterColor
        {
            Red,
            Yellow,
            Green
        }
    }
}
