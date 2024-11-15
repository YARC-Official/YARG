using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using YARG.Core;
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
            ShowPlayer(player);
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
