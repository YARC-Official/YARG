using System;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Serialization;
using UnityEngine.UI;
using YARG.Core;
using YARG.Core.Song;
using YARG.Helpers.Extensions;
using YARG.Song;

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

        private static Color _fcGold = new(1, 208 / 255, 41 / 255);

        private void Awake()
        {
        }

        public void SetInfo(ViewType.ScoreInfo scoreInfo)
        {
            // Set instrument icon
            var icon = Addressables.LoadAssetAsync<Sprite>($"InstrumentIcons[{scoreInfo.Instrument.ToResourceName()}]").WaitForCompletion();
            _instrumentIcon.sprite = icon;

            // Set difficulty icon
            var difficultyValue = scoreInfo.Difficulty switch
            {
                Difficulty.Easy => "E",
                Difficulty.Medium => "M",
                Difficulty.Hard => "H",
                Difficulty.Expert => "X",
                Difficulty.ExpertPlus => "XP",
                _ => ""
            };
            var difficultyIcon = Addressables.LoadAssetAsync<Sprite>($"DifficultyIcons[Diff{difficultyValue}]").WaitForCompletion();
            _difficultyIcon.sprite = difficultyIcon;

            // Set percent value
            _percentText.text = $"{Math.Floor(scoreInfo.Percent * 100)}%";
            _percentText.color = scoreInfo.IsFc ? _fcGold : Color.white;
        }
    }
}