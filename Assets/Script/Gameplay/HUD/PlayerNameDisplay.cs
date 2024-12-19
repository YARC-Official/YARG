using DG.Tweening;
using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using YARG.Core;
using YARG.Core.Logging;
using YARG.Gameplay.Player;
using YARG.Helpers.Extensions;
using YARG.Player;

namespace YARG.Gameplay.HUD
{
    public class PlayerNameDisplay : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI _playerName;
        [SerializeField]
        private Image _instrumentIcon;
        [SerializeField]
        private RawImage _needleIcon;

        private CanvasGroup _canvasGroup;

        public float DisplayTime = 3.0f;
        public float FadeDuration = 0.5f;

        void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
        }

        public void ShowPlayer(YargPlayer player)
        {
            var profile = player.Profile;
            _playerName.text = profile.Name;

            var spriteName = GetSpriteName(profile.CurrentInstrument, profile.HarmonyIndex);
            _instrumentIcon.sprite = Addressables
                .LoadAssetAsync<Sprite>(spriteName)
                .WaitForCompletion();

            StartCoroutine(FadeoutCoroutine());
        }

        private string GetSpriteName(Instrument currentInstrument, byte harmonyIndex)
        {
            if (currentInstrument == Instrument.Harmony)
            {
                return $"HarmonyVocalsIcons[{harmonyIndex + 1}]";
            }

            return $"InstrumentIcons[{currentInstrument.ToResourceName()}]";
        }

        public void ShowPlayer(YargPlayer player, int needleId)
        {
            var textureNeedle = $"VocalNeedleTexture/{needleId}";
            _needleIcon.texture = Addressables.LoadAssetAsync<Texture2D>(textureNeedle).WaitForCompletion();
            _instrumentIcon.color = GetHarmonyColor(player);
            ShowPlayer(player);
        }

        private Color GetHarmonyColor(YargPlayer player)
        {
            if (player.Profile.CurrentInstrument != Instrument.Harmony)
            {
                return Color.white;
            }

            if (player.Profile.HarmonyIndex >= VocalTrack.Colors.Length)
            {
                YargLogger.LogWarning("PlayerNameDisplay", $"Harmony index {player.Profile.HarmonyIndex} is out of bounds.");
                return Color.white;
            }

            return VocalTrack.Colors[player.Profile.HarmonyIndex];
        }

        private IEnumerator FadeoutCoroutine()
        {
            _canvasGroup.alpha = 1f;
            yield return new WaitForSeconds(DisplayTime);
            yield return _canvasGroup.DOFade(0f, FadeDuration).WaitForCompletion();

            gameObject.SetActive(false);
        }
    }
}
