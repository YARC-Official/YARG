﻿using System;
using System.Collections;
using Cysharp.Text;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Core.Engine;

namespace YARG.Gameplay.HUD
{
    public class SoloBox : MonoBehaviour
    {
        [SerializeField]
        private Image           _soloBox;
        [SerializeField]
        private TextMeshProUGUI _soloTopText;
        [SerializeField]
        private TextMeshProUGUI _soloBottomText;
        [SerializeField]
        private TextMeshProUGUI _soloFullText;
        [SerializeField]
        private CanvasGroup     _soloBoxCanvasGroup;

        [Space]
        [SerializeField]
        private Sprite _soloSpriteNormal;
        [SerializeField]
        private Sprite _soloSpritePerfect;
        [SerializeField]
        private Sprite _soloSpriteMessy;

        [SerializeField]
        private TMP_ColorGradient _soloGradientNormal;
        [SerializeField]
        private TMP_ColorGradient _soloGradientPerfect;
        [SerializeField]
        private TMP_ColorGradient _soloGradientMessy;

        private bool _soloEnded;
        private SoloSection _solo;

        private int HitPercent => Mathf.FloorToInt((float) _solo.NotesHit / _solo.NoteCount * 100f);

        private Coroutine _currentCoroutine;

        public void StartSolo(SoloSection solo)
        {
            // Don't even bother if the solo has no points
            if (solo.NoteCount == 0) return;

            _solo = solo;
            _soloEnded = false;
            gameObject.SetActive(true);

            StopCurrentCoroutine();

            _currentCoroutine = StartCoroutine(ShowCoroutine());
        }

        private IEnumerator ShowCoroutine()
        {
            _soloFullText.text = string.Empty;
            _soloBox.sprite = _soloSpriteNormal;

            // Set some dummy text
            _soloTopText.text = "0%";
            _soloBottomText.SetTextFormat("0/{0}", _solo.NoteCount);

            // Fade in the box
            yield return _soloBoxCanvasGroup
                .DOFade(1f, 0.25f)
                .WaitForCompletion();
        }

        private void Update()
        {
            if (_soloEnded) return;

            _soloTopText.SetTextFormat("{0}%", HitPercent);
            _soloBottomText.SetTextFormat("{0}/{1}", _solo.NotesHit, _solo.NoteCount);
        }

        public void EndSolo(int soloBonus, Action endCallback)
        {
            StopCurrentCoroutine();

            _currentCoroutine = StartCoroutine(HideCoroutine(soloBonus, endCallback));
        }

        public void ForceReset()
        {
            StopCurrentCoroutine();
            _soloEnded = true;

            _soloBox.gameObject.SetActive(false);
            _currentCoroutine = null;
            _solo = null;
        }

        private IEnumerator HideCoroutine(int soloBonus, Action endCallback)
        {
            _soloEnded = true;

            // Hide the top and bottom text
            _soloTopText.text = string.Empty;
            _soloBottomText.text = string.Empty;

            // Get the correct gradient and color
            var (sprite, gradient) = HitPercent switch
            {
                >= 100 => (_soloSpritePerfect, _soloGradientPerfect),
                >= 60  => (_soloSpriteNormal, _soloGradientNormal),
                _      => (_soloSpriteMessy, _soloGradientMessy),
            };
            _soloBox.sprite = sprite;
            _soloFullText.colorGradientPreset = gradient;

            // Display final hit percentage
            _soloFullText.SetTextFormat("{0}%", HitPercent);

            yield return new WaitForSeconds(1f);

            // Show performance text
            string resultText = HitPercent switch
            {
                > 100 => "HOW!?",
                  100 => "PERFECT\nSOLO!",
                >= 95 => "AWESOME\nSOLO!",
                >= 90 => "GREAT\nSOLO!",
                >= 80 => "GOOD\nSOLO!",
                >= 70 => "SOLID\nSOLO",
                   69 => "<i>NICE</i>\nSOLO",
                >= 60 => "OKAY\nSOLO",
                >= 0  => "MESSY\nSOLO",
                <  0  => "HOW!?",
            };
            _soloFullText.text = resultText;

            yield return new WaitForSeconds(1f);

            // Show point bonus
            _soloFullText.SetTextFormat("{0:N0}\nPOINTS", soloBonus);

            yield return new WaitForSeconds(1f);

            // Fade out the box
            yield return _soloBoxCanvasGroup
                .DOFade(0f, 0.25f)
                .WaitForCompletion();

            _soloBox.gameObject.SetActive(false);
            _currentCoroutine = null;
            _solo = null;

            endCallback?.Invoke();
        }

        private void StopCurrentCoroutine()
        {
            if (_currentCoroutine != null)
            {
                StopCoroutine(_currentCoroutine);
                _currentCoroutine = null;
            }
        }
    }
}