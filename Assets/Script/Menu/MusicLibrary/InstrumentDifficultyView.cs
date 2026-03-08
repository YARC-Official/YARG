using System;
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
        [SerializeField]
        private Image _instrumentIcon;

        [SerializeField]
        private Image _difficultyIcon;

        [SerializeField]
        private TextMeshProUGUI _percentText;

        private static Color _fcGold = new(1, 208 / 255f, 41 / 255f);


        public void SetInfo(ViewType.ScoreInfo scoreInfo)
        {
            // Set instrument icon
            var icon = Addressables.LoadAssetAsync<Sprite>($"InstrumentIcons[{scoreInfo.Instrument.ToResourceName()}]").WaitForCompletion();
            _instrumentIcon.sprite = icon;

            // Set difficulty icon
            var difficultyIcon = Addressables.LoadAssetAsync<Sprite>($"DifficultyIcons[{scoreInfo.Difficulty.ToString()}]").WaitForCompletion();
            _difficultyIcon.sprite = difficultyIcon;

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

            _percentText.color = scoreInfo.IsFc ? _fcGold : Color.white;
        }
    }
}