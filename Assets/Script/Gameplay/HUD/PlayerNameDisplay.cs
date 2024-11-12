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

        private readonly PerformanceTextScaler _scaler = new (3f);

        public void ShowPlayer(YargPlayer player)
        {
            var profile = player.Profile;
            _playerName.text = profile.Name;
            var instrumentName = profile.CurrentInstrument == Instrument.Harmony ? "harmVocals" + profile.HarmonyIndex : profile.CurrentInstrument.ToResourceName();
            _instrumentIcon.sprite = Addressables
                .LoadAssetAsync<Sprite>($"InstrumentIcons[{instrumentName}]")
                .WaitForCompletion();

            if (_needleIcon != null)
            {
                // NeedleIcon.sprite = player.Needle.Icon;
            }

            StartCoroutine(AnimationCoroutine());
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
