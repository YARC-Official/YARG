using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Core;
using YARG.Core.Chart;
using YARG.Gameplay.Visuals;
using YARG.Helpers.Extensions;
using YARG.Player;
using static YARG.Core.Game.ColorProfile;

namespace YARG.Menu.HighwayConfiguration
{
    public readonly struct HighwayOrderingItemSpec
    {
        // Neither mergeable nor splittable
        // For example, Red Drum
        public HighwayOrderingItemSpec(string name, DrumsHighwayItemIconType type, int colorIndex, DrumsHighwayItem enumeratedValue)
        {
            Name = name;
            Type = type;
            ColorIndex = colorIndex;
            Value = enumeratedValue;
            SplitsInto = null;
            MergesInto = null;
            MergedResult = null;
        }

        // Splittable. Provide the two items that it splits into as a tuple
        // For example, a combined Yellow splits into a Yellow Cymbal and a Yellow Tom
        public HighwayOrderingItemSpec(string name, DrumsHighwayItemIconType type, int colorIndex, DrumsHighwayItem enumeratedValue, (DrumsHighwayItem, DrumsHighwayItem) splitsInto)
        {
            Name = name;
            Type = type;
            ColorIndex = colorIndex;
            Value = enumeratedValue;
            SplitsInto = splitsInto;
            MergesInto = null;
            MergedResult = null;
        }

        // Mergeable. Provide what it merges into, and what the merged result is.
        // For example, a Yellow Cymbal merges into a Yellow Drum to produce a combined Yellow
        public HighwayOrderingItemSpec(string name, DrumsHighwayItemIconType type, int colorIndex, DrumsHighwayItem enumeratedValue, DrumsHighwayItem mergesInto, DrumsHighwayItem mergedResult)
        {
            Name = name;
            Type = type;
            ColorIndex = colorIndex;
            Value = enumeratedValue;
            SplitsInto = null;
            MergesInto = mergesInto;
            MergedResult = mergedResult;
        }

        public string Name { get; }
        public DrumsHighwayItemIconType Type { get; }
        public int ColorIndex { get; }
        public DrumsHighwayItem Value { get; }
        public (DrumsHighwayItem, DrumsHighwayItem)? SplitsInto { get; }
        public DrumsHighwayItem? MergesInto { get; }
        public DrumsHighwayItem? MergedResult { get; }
    }

    public class HighwayOrderingItem : MonoBehaviour
    {
        [SerializeField]
        private Image _icon;
        [SerializeField]
        private TextMeshProUGUI _name;
        [SerializeField]
        private Button _leftButton;
        [SerializeField]
        private Button _rightButton;

        [SerializeField]
        private GameObject _splitOrMerge;
        [SerializeField]
        private TextMeshProUGUI _splitOrMergeButtonText;

        [SerializeField]
        private GameObject _removeDedicatedLanes;
        [SerializeField]
        private TextMeshProUGUI _removeDedicatedLanesButtonText;

        [SerializeField]
        private GameObject _expertPlusOnly;
        [SerializeField]
        private Toggle _expertPlusOnlyToggle;

        [Space]
        [SerializeField]
        private Sprite _drumShape;
        [SerializeField]
        private Sprite _cymbalShape;
        [SerializeField]
        private Sprite _combinedShape;
        [SerializeField]
        private Sprite _kickShape;

        private DrumsHighwayConfigurationMenu _configMenu;
        private HighwayOrderingItemSpec _spec;

        public void Initialize(
            DrumsHighwayConfigurationMenu configMenu,
            HighwayOrderingItemSpec spec,
            IFretColorProvider colorProvider,
            bool isFirst,
            bool isLast
        ) {
            _spec = spec;
            _configMenu = configMenu;
            _name.text = spec.Name;
            _icon.color = colorProvider.GetFretColor(spec.ColorIndex).ToUnityColor();
            _icon.sprite = spec.Type switch
            {
                DrumsHighwayItemIconType.Drum => _drumShape,
                DrumsHighwayItemIconType.Cymbal => _cymbalShape,
                DrumsHighwayItemIconType.Combined => _combinedShape,
                DrumsHighwayItemIconType.Kick => _kickShape,
                _ => throw new ArgumentOutOfRangeException("o no")
            };

            _leftButton.interactable = !isFirst;
            _rightButton.interactable = !isLast;

            if (_spec.SplitsInto is not null)
            {
                _splitOrMerge.gameObject.SetActive(true);
                _splitOrMergeButtonText.text = "Split";
            }
            else if (_spec.MergesInto is not null)
            {
                _splitOrMerge.gameObject.SetActive(true);
                _splitOrMergeButtonText.text = "Merge";
            }
            else
            {
                _splitOrMerge.gameObject.SetActive(false);
            }

            if (spec.Value is DrumsHighwayItem.Kick or DrumsHighwayItem.Kick1x)
            {
                _removeDedicatedLanes.gameObject.SetActive(true);
                _removeDedicatedLanesButtonText.text = spec.Value is DrumsHighwayItem.Kick ? "Remove Dedicated Lane" : "Remove Dedicated Lanes";
            } else
            {
                _removeDedicatedLanes.gameObject.SetActive(false);
            }

            _expertPlusOnlyToggle.isOn = spec.Value is DrumsHighwayItem.Kick2xConditional;
            _expertPlusOnly.gameObject.SetActive(spec.Value is DrumsHighwayItem.Kick2x or DrumsHighwayItem.Kick2xConditional);
        }

        public void MoveLeft() {
            _configMenu.MoveItemLeft(_spec.Value);
        }

        public void MoveRight() {
            _configMenu.MoveItemRight(_spec.Value);
        }

        public void SplitOrMerge()
        {
            if (_spec.MergesInto is not null)
            {
                _configMenu.MergeItemInto(_spec.Value, _spec.MergesInto.Value, _spec.MergedResult.Value);
            }
            else if (_spec.SplitsInto is not null)
            {
                _configMenu.SplitItemInto(_spec.Value, _spec.SplitsInto.Value);
            }
        }

        public void RemoveDedicatedKickLanes()
        {
            _configMenu.RemoveDedicatedKickLanes();
        }

        public void ToggleExpertPlusOnly()
        {
            _configMenu.ToggleExpertPlusOnly();
        }

        private static (int pad, int colorIndex) FOUR_LANE_RED_DRUM = ((int) FourLaneDrumPad.RedDrum, (int) FourLaneDrumsFret.RedDrum);
        private static (int pad, int colorIndex) FOUR_LANE_YELLOW_DRUM = ((int) FourLaneDrumPad.YellowDrum, (int) FourLaneDrumsFret.YellowDrum);
        private static (int pad, int colorIndex) FOUR_LANE_BLUE_DRUM = ((int) FourLaneDrumPad.BlueDrum, (int) FourLaneDrumsFret.BlueDrum);
        private static (int pad, int colorIndex) FOUR_LANE_GREEN_DRUM = ((int) FourLaneDrumPad.GreenDrum, (int) FourLaneDrumsFret.GreenDrum);
        private static (int pad, int colorIndex) FOUR_LANE_YELLOW_CYMBAL = ((int) FourLaneDrumPad.YellowCymbal, (int) FourLaneDrumsFret.YellowCymbal);
        private static (int pad, int colorIndex) FOUR_LANE_BLUE_CYMBAL = ((int) FourLaneDrumPad.BlueCymbal, (int) FourLaneDrumsFret.BlueCymbal);
        private static (int pad, int colorIndex) FOUR_LANE_GREEN_CYMBAL = ((int) FourLaneDrumPad.GreenCymbal, (int) FourLaneDrumsFret.GreenCymbal);

        private static (int pad, int colorIndex) FIVE_LANE_RED = ((int) FiveLaneDrumPad.Red, (int) FiveLaneDrumsFret.Red);
        private static (int pad, int colorIndex) FIVE_LANE_YELLOW = ((int) FiveLaneDrumPad.Yellow, (int) FiveLaneDrumsFret.Yellow);
        private static (int pad, int colorIndex) FIVE_LANE_BLUE = ((int) FiveLaneDrumPad.Blue, (int) FiveLaneDrumsFret.Blue);
        private static (int pad, int colorIndex) FIVE_LANE_ORANGE = ((int) FiveLaneDrumPad.Orange, (int) FiveLaneDrumsFret.Orange);
        private static (int pad, int colorIndex) FIVE_LANE_GREEN = ((int) FiveLaneDrumPad.Green, (int) FiveLaneDrumsFret.Green);

        public static Dictionary<DrumsHighwayItem, List<(int pad, int colorIndex)>> HighwayOrderingInfoMap = new()
        {
            { DrumsHighwayItem.FourLaneRed, new() { FOUR_LANE_RED_DRUM } },

            { DrumsHighwayItem.FourLaneYellow, new() { FOUR_LANE_YELLOW_DRUM, FOUR_LANE_YELLOW_CYMBAL } },
            { DrumsHighwayItem.FourLaneYellowCymbal, new() { FOUR_LANE_YELLOW_CYMBAL } },
            { DrumsHighwayItem.FourLaneYellowDrum, new() { FOUR_LANE_YELLOW_DRUM } },

            { DrumsHighwayItem.FourLaneBlue, new() { FOUR_LANE_BLUE_DRUM, FOUR_LANE_BLUE_CYMBAL } },
            { DrumsHighwayItem.FourLaneBlueCymbal, new() { FOUR_LANE_BLUE_CYMBAL } },
            { DrumsHighwayItem.FourLaneBlueDrum, new() { FOUR_LANE_BLUE_DRUM } },

            { DrumsHighwayItem.FourLaneGreen, new() { FOUR_LANE_GREEN_DRUM, FOUR_LANE_GREEN_CYMBAL } },
            { DrumsHighwayItem.FourLaneGreenDrum, new() { FOUR_LANE_GREEN_DRUM } },
            { DrumsHighwayItem.FourLaneGreenCymbal, new() { FOUR_LANE_GREEN_CYMBAL } },

            { DrumsHighwayItem.FiveLaneRed, new() { FIVE_LANE_RED } },
            { DrumsHighwayItem.FiveLaneYellow, new() { FIVE_LANE_YELLOW } },
            { DrumsHighwayItem.FiveLaneBlue, new() { FIVE_LANE_BLUE } },
            { DrumsHighwayItem.FiveLaneOrange, new() { FIVE_LANE_ORANGE } },
            { DrumsHighwayItem.FiveLaneGreen, new() { FIVE_LANE_GREEN } },
        };
    }
}
