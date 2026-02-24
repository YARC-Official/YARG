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
using YARG.Core.Game;
using YARG.Gameplay.Player;
using YARG.Gameplay.Visuals;
using YARG.Helpers;
using YARG.Helpers.Extensions;
using YARG.Player;
using static UnityEditor.Progress;
using static YARG.Core.Game.ColorProfile;

namespace YARG.Menu.HighwayConfiguration
{
    public struct HighwayOrderingItemSpec
    {
        public string Name { get; set; }
        public string LeftyName { get; set; }
        public DrumsHighwayItemIconType Type { get; set;  }
        public int ColorIndex { get; set; }
        public DrumsHighwayItem Value { get; set; }
        public (DrumsHighwayItem, DrumsHighwayItem)? SplitsInto { get; set; }
        public DrumsHighwayItem? MergesInto { get; set; }
        public DrumsHighwayItem? MergedResult { get; set; }
    }

    public class DrumsHighwayItemView : MonoBehaviour
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
        public DrumsHighwayItem Item { get; set; }
        private int _index => _configMenu.GetItemIndex(Item);

        public void Initialize(
            DrumsHighwayConfigurationMenu configMenu,
            DrumsHighwayItem item
        ) {
            _configMenu = configMenu;
            Item = item;
            Render();
        }

        public void Render()
        {
            _spec = _configMenu.Specs[Item];
            _name.text = _configMenu.Lefty ? _spec.LeftyName : _spec.Name;

            var colorIndex = DrumsColorHelpers.ApplyHandednessToColor(_spec.ColorIndex, _configMenu.Lefty, _configMenu.SplitKicksExist, _configMenu.Instrument);

            _icon.color = _configMenu.ColorProvider.GetFretColor(colorIndex).ToUnityColor();
            _icon.sprite = _spec.Type switch
            {
                DrumsHighwayItemIconType.Drum => _drumShape,
                DrumsHighwayItemIconType.Cymbal => _cymbalShape,
                DrumsHighwayItemIconType.Combined => _combinedShape,
                DrumsHighwayItemIconType.Kick => _kickShape,
                _ => throw new ArgumentOutOfRangeException("o no")
            };

            _leftButton.interactable = _index != (_configMenu.Lefty ? _configMenu.HighwayOrdering.Count - 1 : 0);
            _rightButton.interactable = _index != (_configMenu.Lefty ? 0 : _configMenu.HighwayOrdering.Count - 1);

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

            if (Item is DrumsHighwayItem.Kick or DrumsHighwayItem.Kick1x)
            {
                _removeDedicatedLanes.gameObject.SetActive(true);
                _removeDedicatedLanesButtonText.text = Item is DrumsHighwayItem.Kick ? "Remove Dedicated Lane" : "Remove Dedicated Lanes";
            }
            else
            {
                _removeDedicatedLanes.gameObject.SetActive(false);
            }

            _expertPlusOnly.gameObject.SetActive(Item is DrumsHighwayItem.Kick2x or DrumsHighwayItem.Kick2xConditional);
            _expertPlusOnlyToggle.SetIsOnWithoutNotify(Item is DrumsHighwayItem.Kick2xConditional);
        }

        public void MoveLeft() {
            if (_configMenu.Lefty)
            {
                _configMenu.IncrementItemPosition(Item);
            }
            else
            {
                _configMenu.DecrementItemPosition(Item);
            }
        }

        public void MoveRight() {
            if (_configMenu.Lefty)
            {
                _configMenu.DecrementItemPosition(Item);
            }
            else
            {
                _configMenu.IncrementItemPosition(Item);
            }
        }

        public void SplitOrMerge()
        {
            if (_spec.MergesInto is not null)
            {
                _configMenu.MergeItemInto(Item, _spec.MergesInto.Value, _spec.MergedResult.Value);
            }
            else if (_spec.SplitsInto is not null)
            {
                _configMenu.SplitItemInto(Item, _spec.SplitsInto.Value);
            }
        }

        public void RemoveDedicatedKickLanes()
        {
            _configMenu.RemoveDedicatedKickLanes();
        }

        public void ToggleExpertPlusOnly()
        {
            _configMenu.ToggleExpertPlusOnly(this);
        }

        // Technically we should have separate 4L and 5L kicks, but in practice it doesn't matter
        private static (int pad, int colorIndex) KICK = ((int) FourLaneDrumPad.Kick, (int) FourLaneDrumsFret.Kick);
        private static (int pad, int colorIndex) DOUBLE_KICK = (DrumsPlayer.DOUBLE_KICK_FRET_INDEX, (int) FourLaneDrumsFret.DoubleKick);

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
            { DrumsHighwayItem.Kick, new() { KICK, DOUBLE_KICK } },
            { DrumsHighwayItem.Kick1x, new() { KICK } },
            { DrumsHighwayItem.Kick2x, new() { DOUBLE_KICK } },
            { DrumsHighwayItem.Kick2xConditional, new() { DOUBLE_KICK } },

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
