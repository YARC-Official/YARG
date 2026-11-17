using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;
using YARG.Core;
using YARG.Core.Logging;
using YARG.Gameplay.Player;
using YARG.Helpers.Extensions;
using YARG.Player;
using YARG.Localization;
using YARG.Settings;

namespace YARG.Gameplay.HUD
{
    public class PlayerNameDisplay : GameplayBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI _playerName;
        [SerializeField]
        private Image _instrumentIcon;
        [SerializeField]
        private RawImage _needleIcon;
        [SerializeField]
        private RawImage _playerAvatar;

        private CanvasGroup _canvasGroup;
        private readonly List<RawImage> _additionalNeedles = new();
        private readonly List<TextMeshProUGUI> _missingMicrophoneBadges = new();
        private readonly List<AsyncOperationHandle<Texture2D>> _needleTextures = new();
        private const float NEEDLE_SPACING = 4f;
        private TextMeshProUGUI _microphoneWarning;
        private bool _hasMissingMicrophones;

        public float DisplayTime = 3.0f;
        public float FadeDuration = 0.5f;

        protected override void GameplayAwake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f;
        }

        public void ShowPlayer(YargPlayer player)
        {
            if (!ShouldShowPlayer())
            {
                return;
            }

            var profile = player.Profile;
            _playerName.text = profile.Name;

            if (profile.Avatar != null)
            {
                _instrumentIcon.sprite = Addressables
                    .LoadAssetAsync<Sprite>("BlankInstrumentIcon")
                    .WaitForCompletion();
                _playerAvatar.texture = profile.Avatar.LoadTexture(false);
                _playerAvatar.gameObject.SetActive(true);
            }
            else
            {
                _playerAvatar.gameObject.SetActive(false);
                var spriteName = player.GetInstrumentSprite(GameManager.Song);
                _instrumentIcon.sprite = Addressables
                    .LoadAssetAsync<Sprite>(spriteName)
                    .WaitForCompletion();
            }

            StartCoroutine(FadeoutCoroutine());
        }

        public void ShowPlayer(YargPlayer player, IReadOnlyList<int> needleIds, IReadOnlyList<bool> connectedSlots = null)
        {
            _hasMissingMicrophones = connectedSlots != null && connectedSlots.Any(connected => !connected);
            if (!ShouldShowPlayer())
            {
                return;
            }

            SetNeedleIcons(needleIds, connectedSlots);
            _instrumentIcon.color = player.GetGameplayIconColor();
            ShowPlayer(player);
            if (_hasMissingMicrophones)
            {
                _microphoneWarning = Instantiate(_playerName, _playerName.transform.parent);
                _microphoneWarning.enableAutoSizing = false;
                _microphoneWarning.fontSize = _playerName.fontSize * 0.7f;
                _microphoneWarning.richText = false;
                _microphoneWarning.alignment = TextAlignmentOptions.TopLeft;
                _microphoneWarning.text = Localize.KeyFormat("Gameplay.Vocals.MicrophonesAvailable",
                    connectedSlots.Count(connected => connected), connectedSlots.Count);
                var rect = _microphoneWarning.rectTransform;
                float left = _needleIcon.rectTransform.localPosition.x - _needleIcon.rectTransform.rect.width / 2;
                float right = _playerName.rectTransform.localPosition.x + _playerName.rectTransform.rect.width / 2;
                float bottom = _needleIcon.rectTransform.localPosition.y - _needleIcon.rectTransform.rect.height / 2 -
                    (needleIds.Count - 1) * (_needleIcon.rectTransform.rect.height + NEEDLE_SPACING);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.sizeDelta = new Vector2(right - left, 120);
                rect.localPosition = new Vector3((left + right) / 2, bottom - 8, 0);
            }
        }

        private void SetNeedleIcons(IReadOnlyList<int> needleIds, IReadOnlyList<bool> connectedSlots)
        {
            ClearNeedleIcons();
            float spacing = _needleIcon.rectTransform.rect.height + NEEDLE_SPACING;
            for (int mic = 0; mic < needleIds.Count; mic++)
            {
                var icon = mic == 0 ? _needleIcon : Instantiate(_needleIcon, _needleIcon.transform.parent);
                if (mic > 0)
                {
                    icon.rectTransform.localPosition = _needleIcon.rectTransform.localPosition +
                        new Vector3(0, -mic * spacing, 0);
                    _additionalNeedles.Add(icon);
                }
                var texture = Addressables.LoadAssetAsync<Texture2D>($"VocalNeedleTexture/{needleIds[mic]}");
                _needleTextures.Add(texture);
                icon.texture = texture.WaitForCompletion();
                bool connected = connectedSlots == null || connectedSlots[mic];
                icon.color = connected ? Color.white : new Color(1, 1, 1, 0.25f);
                if (!connected)
                {
                    var badge = Instantiate(_playerName, _playerName.transform.parent);
                    badge.text = badge.font.HasCharacter('×') ? "×" : "X";
                    badge.enableAutoSizing = false;
                    badge.fontSize = _playerName.fontSize;
                    badge.color = Color.white;
                    badge.alignment = TextAlignmentOptions.Center;
                    badge.overflowMode = TextOverflowModes.Overflow;
                    badge.margin = Vector4.zero;
                    badge.raycastTarget = false;
                    var rect = badge.rectTransform;
                    float size = Mathf.Max(icon.rectTransform.rect.height, badge.fontSize);
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    rect.sizeDelta = new Vector2(size, size);
                    rect.localPosition = icon.rectTransform.localPosition +
                        new Vector3(-icon.rectTransform.rect.width / 2 - size / 2 - NEEDLE_SPACING, 0, 0);
                    _missingMicrophoneBadges.Add(badge);
                }
            }
        }

        private void ClearNeedleIcons()
        {
            if (_microphoneWarning != null)
            {
                _microphoneWarning.gameObject.SetActive(false);
                Destroy(_microphoneWarning.gameObject);
                _microphoneWarning = null;
            }
            foreach (var icon in _additionalNeedles)
            {
                icon.gameObject.SetActive(false);
                Destroy(icon.gameObject);
            }
            _additionalNeedles.Clear();
            foreach (var badge in _missingMicrophoneBadges)
            {
                badge.gameObject.SetActive(false);
                Destroy(badge.gameObject);
            }
            _missingMicrophoneBadges.Clear();
            foreach (var texture in _needleTextures) Addressables.Release(texture);
            _needleTextures.Clear();
        }

        protected override void GameplayDestroy()
        {
            ClearNeedleIcons();
        }

        public void AlignNameAndNeedleWith(RectTransform feedback)
        {
            // The instrument badge/avatar remains beside the combo meter.
            float y = transform.InverseTransformPoint(feedback.TransformPoint(feedback.rect.center)).y;
            var namePosition = _playerName.rectTransform.localPosition;
            float offset = y - namePosition.y;
            namePosition.y = y;
            _playerName.rectTransform.localPosition = namePosition;
            _needleIcon.rectTransform.localPosition += new Vector3(0, offset, 0);
            foreach (var icon in _additionalNeedles)
                icon.rectTransform.localPosition += new Vector3(0, offset, 0);
            foreach (var badge in _missingMicrophoneBadges)
                badge.rectTransform.localPosition += new Vector3(0, offset, 0);
            if (_microphoneWarning != null)
                _microphoneWarning.rectTransform.localPosition += new Vector3(0, offset, 0);
        }

        private bool ShouldShowPlayer()
        {
            return !GameManager.IsPractice && (_hasMissingMicrophones || SettingsManager.Settings.ShowPlayerNameWhenStartingSong.Value);
        }

        private IEnumerator FadeoutCoroutine()
        {
            _canvasGroup.alpha = 1f;
            yield return new WaitForSeconds(_hasMissingMicrophones ? Mathf.Max(DisplayTime, 6f) : DisplayTime);
            yield return _canvasGroup.DOFade(0f, FadeDuration).SetLink(gameObject).WaitForCompletion();

            gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            _canvasGroup.DOKill();

            if (_playerAvatar.texture != null)
            {
                Destroy(_playerAvatar.texture);
                _playerAvatar.texture = null;
            }
        }
    }
}
