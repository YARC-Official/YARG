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
        private Tweener _bandColorTweener;
        private Tweener _bandFillTweener;
        private float[] _previousPlayerHappiness;
        private float _previousBandHappiness;
        private Vector2 _offsetVector = new Vector2(0, 0);

        private Vector2[] _playerPositions;

        private float _containerHeight;


        // TODO: Should probably make a more specific class we can reference here
        private EngineManager _engineManager;
        private GameManager _gameManager;

        private List<EngineManager.EngineContainer> _players = new();

        private const int SPRITE_OFFSET = -35;
        private const float SPRITE_SIZE = 35;


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
            _containerHeight = _sliderContainer.rect.height;

            // attach the slider instances to the scene and apply the correct icon
            for (int i = 0; i < _players.Count; i++)
            {
                float offset = (float) (SPRITE_OFFSET * (Math.Floor(i / (float) 2) + 1));
                _playerSliders[i] = Instantiate(_sliderPrefab, _sliderContainer);

                // TODO: Fix this, it reads like shit
                if (i % 2 == 0)
                {
                    // _playerSliders[i].handleRect.offsetMin =
                    //     new Vector2(offset, _playerSliders[i].handleRect.offsetMin.y);
                    // _playerSliders[i].handleRect.anchoredPosition =
                    //     new Vector2(offset, _playerSliders[i].handleRect.anchoredPosition.y);
                    _xposTweeners[i] = _playerSliders[i].handleRect.DOAnchorPosX(offset, 0.125f).SetAutoKill(false);
                }
                else
                {
                    // _playerSliders[i].handleRect.offsetMax =
                    //     new Vector2(offset * -1, _playerSliders[i].handleRect.offsetMax.y);
                    // _playerSliders[i].handleRect.anchoredPosition =
                    //     new Vector2(offset * -1, _playerSliders[i].handleRect.anchoredPosition.y);
                    _xposTweeners[i] = _playerSliders[i].handleRect.DOAnchorPosX(offset * -1, 0.125f).SetAutoKill(true);
                }

                _playerPositions[i] = _playerSliders[i].handleRect.transform.position;
                // _playerPositions[i] = _playerSliders[i].handleRect.anchoredPosition;

                var handleImage = _playerSliders[i].handleRect.GetComponentInChildren<Image>();
                var spriteName = $"InstrumentIcons[{_players[i].Instrument.ToResourceName()}]";
                var sprite = Addressables.LoadAssetAsync<Sprite>(spriteName).WaitForCompletion();
                handleImage.sprite = sprite;
                _playerSliders[i].value = 0.01f;
                _playerSliders[i].gameObject.SetActive(true);

                // Cached for reuse because starting a new tween generates garbage
                _happinessTweeners[i] = _playerSliders[i].DOValue(_players[i].Happiness, 0.5f).SetAutoKill(false);
                // SPRITE_OFFSET here is just a placeholder, we're initializing without actually doing anything
                _previousPlayerHappiness[i] = _players[i].Happiness;
            }

            YargLogger.LogDebug("Initialized fail meter");
        }

        // Update is called once per frame
        private void Update()
        {
            // Don't crash the whole game if we didn't get initialized and still manage to somehow become active
            // TODO: This is actually happening somehow (at least sometimes) WTF?
            if (_engineManager == null)
            {
                // YargLogger.LogDebug("FailMeter not initialized");
                return;
            }

            // No need for any of this if we're paused anyway
            // TODO: While we should be doing this, it is also a dirty hack to keep from spawning a massive
            //  number of unnecessary tweens since update gets called a lot more often when paused for whatever reason
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
                    if (_bandColorTweener != null && _bandColorTweener.active)
                    {
                        _bandColorTweener.Kill();
                    }
                    _fillImage.color = Color.black;
                    _bandColorTweener = _fillImage.DOColor(Color.red, 0.25f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
                    // _fillImage.color = Color.red;
                }
                else if (happiness >= 0.33f && _previousBandHappiness < 0.33f)
                {
                    if (_bandColorTweener != null && _bandColorTweener.active)
                    {
                        _bandColorTweener.Kill();
                    }
                    _bandColorTweener = _fillImage.DOColor(Color.green, 0.25f).SetAutoKill(true);
                    // _fillImage.color = Color.green;
                }

                _fillImage.DOFillAmount(happiness, 0.125f);
                // _fillImage.fillAmount = happiness;

                _previousBandHappiness = _engineManager.Happiness;

            }

            // TODO: Somewhere in here we need to adjust the X pos of the children when they are overlapping
            //  (left for even numbered, right for odd numbered or vv, I forget)
            for (var i = 0; i < _players.Count; i++)
            {
                // if (_previousPlayerHappiness[i] != _players[i].Happiness)
                // {
                    // Convert happiness to a new y value
                    float newY = (_containerHeight * _players[i].Happiness) + (_sliderContainer.position.y - (_containerHeight / 2));
                    int overlap = 0;
                    // Check if we will overlap another icon
                    for (var j = i; j < _players.Count; j += 2)
                    {
                        if (j == i)
                        {
                            // Ignore self
                            continue;
                        }

                        if (newY >= _playerPositions[j].y - (SPRITE_SIZE / 1.25) &&
                            newY <= _playerPositions[j].y + (SPRITE_SIZE / 1.25))
                        {
                            overlap++;
                        }
                    }

                    // Move the icon SPRITE_OFFSET * overlap in the appropriate direction

                    float offset = SPRITE_OFFSET * Math.Max(1, overlap + 1);
                    // TODO: This won't actually work once there are more than two players on a side
                    offset *= Math.Max(1, (int) Math.Floor((float) i / 2));
                    // float offset = (float) (SPRITE_OFFSET * (Math.Floor(i / (float) 2) + 1));
                    // TODO: Figure out how to tween this move
                    if (i % 2 == 0)
                    {
                        // _playerSliders[i].handleRect.offsetMin =
                        //     new Vector2(offset, _playerSliders[i].handleRect.offsetMin.y);
                        _xposTweeners[i] = _playerSliders[i].handleRect.DOAnchorPosX(offset, 0.125f);
                        _offsetVector.x = offset;
                        _offsetVector.y = _playerSliders[i].handleRect.anchoredPosition.y;
                        // _xposTweeners[i].ChangeEndValue(_offsetVector).Play();
                    }
                    else
                    {
                        // _playerSliders[i].handleRect.offsetMax =
                        //     new Vector2(offset * -1, _playerSliders[i].handleRect.offsetMax.y);
                        _xposTweeners[i] = _playerSliders[i].handleRect.DOAnchorPosX(offset * -1, 0.125f);
                        _offsetVector.x = offset * -1;
                        _offsetVector.y = _playerSliders[i].handleRect.anchoredPosition.y;
                        // _xposTweeners[i].ChangeEndValue(_offsetVector).Play();
                    }

                    // We only need the x, y so it's fine that we're converting Vector3 to Vector2 here
                    _playerPositions[i] = _playerSliders[i].handleRect.transform.position;
                    // _playerPositions[i] = _playerSliders[i].handleRect.anchoredPosition;

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
                // }
            }
        }

        private void OnDisable()
        {
            // Make sure the tweens are dead
            _bandColorTweener?.Kill();
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
