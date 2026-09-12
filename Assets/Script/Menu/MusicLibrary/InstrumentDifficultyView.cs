using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using YARG.Core.Game;
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
        private Image _difficultyRing;

        [SerializeField]
        private TextMeshProUGUI _percentText;

        private static readonly Color FcGold = new(1, 208 / 255f, 41 / 255f);
        private static readonly Color DefaultEngineColor = Color.white;
        private static readonly Color CasualEngineColor = new(0.9f, 0.3f, 0.9f);
        private static readonly Color PrecisionEngineColor = new(1f, 0.9f, 0f);
        private static readonly Color SoloTapsEngineColor = new(0.5411765f, 0.1686275f, 0.8862746f);
        private static readonly Color CustomEngineColor = new(1f, 0.25f, 0.25f);


        public void SetInfo(ViewType.ScoreInfo scoreInfo)
        {
            // Set width
            var rect = GetComponent<RectTransform>();
            var length = SettingsManager.Settings.ShowPercentDecimals.Value ? 150 : 130;
            rect.sizeDelta = new Vector2(length, rect.sizeDelta.y);

            // Set instrument icon
            _instrumentIcon.sprite = GetSprite($"InstrumentIcons[{scoreInfo.Instrument.ToResourceName()}]");

            // Set difficulty icon
            _difficultyIcon.sprite = GetSprite($"DifficultyIcons[{scoreInfo.Difficulty.ToString()}]");
            _difficultyIcon.color = Color.white;
            _difficultyRing.color = GetEngineColor(scoreInfo.EnginePresetId);

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

        private static Color GetEngineColor(Guid enginePresetId) => enginePresetId switch
        {
            var id when id == Guid.Empty || id == EnginePreset.Default.Id => DefaultEngineColor,
            var id when id == EnginePreset.Casual.Id => CasualEngineColor,
            var id when id == EnginePreset.Precision.Id => PrecisionEngineColor,
            var id when id == EnginePreset.SoloTaps.Id => SoloTapsEngineColor,
            _ => CustomEngineColor
        };
    }
}