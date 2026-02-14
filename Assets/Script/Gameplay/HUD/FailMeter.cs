using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Serialization;
using UnityEngine.UI;
using YARG.Core.Engine;
using YARG.Core.Game;
using YARG.Core.Logging;
using YARG.Helpers.Extensions;

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
        private Slider _needlePrefab;
        [SerializeField]
        private RectTransform _sliderContainer;

        private Slider[]  _playerSliders;
        private Slider[]  _needleSliders;
        private Tweener[] _playerHappinessTweeners = Array.Empty<Tweener>();
        private Tweener[] _needleHappinessTweeners = Array.Empty<Tweener>();
        private Tweener[] _xposTweeners            = Array.Empty<Tweener>();
        private Tweener   _meterRedTweener;
        private Tweener   _meterYellowTweener;
        private Tweener   _meterGreenTweener;
        private Tweener   _bandFillTweener;
        private Tweener   _meterPositionTweener;
        private float[]   _previousPlayerHappiness;
        private float     _previousBandHappiness;

        private MeterColor _previousMeterColor;

        private Vector2[] _xPosVectors;

        private bool _intendedActive;


        // TODO: Should probably make a more specific class we can reference here
        private EngineManager _engineManager;
        private GameManager _gameManager;

        private readonly List<EngineManager.EngineContainer> _players = new();

        // Allows some overlap
        private const float HAPPINESS_COLLISION_RANGE = 0.06f;
        private const float SPRITE_OVERLAP_OFFSET     = 28f;
        private const float SPRITE_INITIAL_OFFSET     = 42f;

        // GameManager will have to initialize us
        public void Initialize(EngineManager engineManager, GameManager gameManager)
        {
            _gameManager = gameManager;
            _engineManager = engineManager;
            _players.AddRange(engineManager.Engines);

            _playerSliders = new Slider[_players.Count];
            _needleSliders = new Slider[_players.Count];
            _xposTweeners = new Tweener[_players.Count];
            _playerHappinessTweeners = new Tweener[_players.Count];
            _needleHappinessTweeners = new Tweener[_players.Count];
            _previousPlayerHappiness = new float[_players.Count];
            _xPosVectors = new Vector2[_players.Count];

            // Cache tweens for later use
            _meterRedTweener = _fillImage.DOColor(ColorProfile.DefaultRed.ToUnityColor(), 0.25f).
                SetLoops(-1, LoopType.Yoyo).
                SetEase(Ease.InOutSine).
                SetAutoKill(false).
                Pause().
                SetLink(_fillImage.gameObject);

            _meterYellowTweener = _fillImage.DOColor(ColorProfile.DefaultYellow.ToUnityColor(), 0.25f).
                SetAutoKill(false).
                Pause().
                SetLink(_fillImage.gameObject);

            _meterGreenTweener = _fillImage.DOColor(ColorProfile.DefaultGreen.ToUnityColor(), 0.25f).
                SetAutoKill(false).
                Pause().
                SetLink(_fillImage.gameObject);

            // 0.8f is an arbitrary placeholder
            _bandFillTweener = _fillImage.DOFillAmount(0.8f, 0.125f).
                SetAutoKill(false).
                SetLink(_fillImage.gameObject);

            // This is set up to move the container offscreen, but may later be used to move it back on
            _meterPositionTweener = _meterContainer.transform.DOMoveY(-400f, 0.5f).
                SetAutoKill(false).
                Pause().
                SetLink(_meterContainer);


            // attach the slider instances to the scene and apply the correct icon
            for (int i = _players.Count - 1; i >= 0; i--)
            {
                _playerSliders[i] = Instantiate(_sliderPrefab, _sliderContainer);
                _needleSliders[i] = Instantiate(_needlePrefab, _sliderContainer);
                // y value is ignored, so it is ok that it is zero here
                var xOffset = SPRITE_INITIAL_OFFSET + (SPRITE_OVERLAP_OFFSET * i);
                _xPosVectors[i] = new Vector2(xOffset, 0);

                _xposTweeners[i] = _playerSliders[i].handleRect.DOAnchorPosX(_xPosVectors[i].x, 0.125f).SetAutoKill(false).SetLink(_playerSliders[i].gameObject);
                _needleSliders[i].handleRect.DOAnchorPosX(SPRITE_INITIAL_OFFSET, 0.125f).SetAutoKill(false).SetLink(_needleSliders[i].gameObject);

                var handleImage = _playerSliders[i].handleRect.GetComponentInChildren<Image>();
                var spriteName = _players[i].GetInstrumentSprite();

                var sprite = Addressables.LoadAssetAsync<Sprite>(spriteName).WaitForCompletion();
                handleImage.sprite = sprite;
                handleImage.color = _players[i].GetHarmonyColor();

                _playerSliders[i].value = 0.01f;
                _needleSliders[i].value = 0.01f;
                _playerSliders[i].gameObject.SetActive(true);
                _needleSliders[i].gameObject.SetActive(true);

                // Cached for reuse because starting a new tween generates garbage
                _playerHappinessTweeners[i] = _playerSliders[i].DOValue(_players[i].Happiness, 0.5f).SetAutoKill(false).SetLink(_playerSliders[i].gameObject);
                _needleHappinessTweeners[i] = _needleSliders[i].DOValue(_players[i].Happiness, 0.5f).SetAutoKill(false).SetLink(_needleSliders[i].gameObject);
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

            for (var i = _players.Count - 1; i >= 0; i--)
            {
                int overlap = 0;
                // Check if we will overlap another icon
                for (var j = i; j >= 0; j--)
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

                // This we can not do if the current player's happiness hasn't changed
                if (_previousPlayerHappiness[i] != _players[i].Happiness)
                {
                    _playerHappinessTweeners[i].ChangeValues(_playerSliders[i].value, _players[i].Happiness, 0.1f);
                    _needleHappinessTweeners[i].ChangeValues(_needleSliders[i].value, _players[i].Happiness, 0.1f);

                    // Not sure if strictly necessary, but it seems like good practice to not try to play a playing tween
                    if (_playerHappinessTweeners[i].IsComplete())
                    {
                        _playerHappinessTweeners[i].Play();
                    }
                    else
                    {
                        _playerHappinessTweeners[i].Restart();
                    }

                    if (_needleHappinessTweeners[i].IsComplete())
                    {
                        _needleHappinessTweeners[i].Play();
                    }
                    else
                    {
                        _needleHappinessTweeners[i].Restart();
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
                < 0.333f => MeterColor.Red,
                < 0.666f => MeterColor.Yellow,
                _        => MeterColor.Green
            };
        }

        private enum MeterColor
        {
            Red,
            Yellow,
            Green
        }
    }
}
