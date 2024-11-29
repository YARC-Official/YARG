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
        private Image _needleIcon;

        private readonly PerformanceTextScaler _scaler = new(3f);

        public void ShowPlayer(YargPlayer player)
        {
            var profile = player.Profile;
            _playerName.text = profile.Name;

            var spriteName = GetSpriteName(profile.CurrentInstrument, profile.HarmonyIndex);
            _instrumentIcon.sprite = Addressables
                .LoadAssetAsync<Sprite>(spriteName)
                .WaitForCompletion();

            StartCoroutine(AnimationCoroutine());
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
            var materialPath = $"VocalNeedle/{needleId}";
            _needleIcon.material = Addressables.LoadAssetAsync<Material>(materialPath).WaitForCompletion();
            _instrumentIcon.color = GetHarmonyColor(player);
            ShowPlayer(player);
        }

        // TODO: Temporary until color profiles for vocals. Duplicated from VocalTrack class.
        public readonly Color[] HarmonyColors =
        {
            new(0f, 0.800f, 1f, 1f),
            new(1f, 0.522f, 0f, 1f),
            new(1f, 0.859f, 0f, 1f)
        };

        private Color GetHarmonyColor(YargPlayer player)
        {
            if (player.Profile.CurrentInstrument != Instrument.Harmony)
            {
                return Color.white;
            }

            if (player.Profile.HarmonyIndex >= HarmonyColors.Length)
            {
                YargLogger.LogWarning("PlayerNameDisplay", $"Harmony index {player.Profile.HarmonyIndex} is out of bounds.");
                return Color.white;
            }

            return HarmonyColors[player.Profile.HarmonyIndex];
        }

        private IEnumerator AnimationCoroutine()
        {
            gameObject.SetActive(true);
            _scaler.ResetAnimationTime();

            while (_scaler.AnimTimeRemaining > 0f)
            {
                _scaler.AnimTimeRemaining -= Time.deltaTime;
                float scale = _scaler.PerformanceTextScale();

                gameObject.transform.localScale = new Vector3(scale, scale, scale);
                yield return null;
            }

            gameObject.SetActive(false);
        }
    }
}
