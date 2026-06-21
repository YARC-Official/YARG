using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using YARG.Helpers.Extensions;
using YARG.Settings;

namespace YARG.Menu.MusicLibrary
{
    public class InstrumentDifficultyView : MonoBehaviour
    {
        private static readonly Dictionary<string, Sprite> SpriteCache = new();

        [SerializeField]
        private Image _instrumentIcon;

        [SerializeField]
        private Image _difficultyIcon;

        [SerializeField]
        private TextMeshProUGUI _percentText;

        private static readonly Color FcGold = new(1, 208 / 255f, 41 / 255f);


        public void SetInfo(ViewType.ScoreInfo scoreInfo)
        {
            // Set width
            var rect = GetComponent<RectTransform>();
            var length = SettingsManager.Settings.ShowPercentDecimals.Value ? 150 : 130;
            GetComponent<RectTransform>().sizeDelta = new Vector2(length, rect.sizeDelta.y);

            // Set instrument icon
            _instrumentIcon.sprite = GetSprite($"InstrumentIcons[{scoreInfo.Instrument.ToResourceName()}]");

            // Set difficulty icon
            _difficultyIcon.sprite = GetSprite($"DifficultyIcons[{scoreInfo.Difficulty.ToString()}]");

            // Set percent value
            if (SettingsManager.Settings.ShowPercentDecimals.Value)
            {
                var percent = Mathf.Floor(scoreInfo.Percent * 1000f) / 10f;
                _percentText.text = $"{percent:0.0}%";
            }
            else
            {
                _percentText.text = $"{Mathf.FloorToInt(scoreInfo.Percent * 100f)}%";
            }

            _percentText.color = scoreInfo.IsFc ? FcGold : Color.white;
        }

        private static Sprite GetSprite(string assetKey)
        {
            if (!SpriteCache.TryGetValue(assetKey, out var sprite))
            {
                SpriteCache[assetKey] = sprite = Addressables.LoadAssetAsync<Sprite>(assetKey).WaitForCompletion();
            }

            return sprite;
        }
    }
}