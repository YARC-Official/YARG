using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Core;
using YARG.Core.Chart;
using YARG.Core.Input;
using YARG.Helpers;
using YARG.Helpers.Extensions;
using YARG.Localization;
using static YARG.Core.Game.ColorProfile;
using static YARG.Gameplay.Player.DrumsPlayer;

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
        private TextMeshProUGUI _expertPlusOnlyText;
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
            _name.text = Localize.Key("Menu.HighwayOrdering", _configMenu.Lefty ? _spec.LeftyName : _spec.Name);

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
                _removeDedicatedLanesButtonText.text = Localize.Key("Menu.HighwayOrdering",
                    Item is DrumsHighwayItem.Kick ? "RemoveDedicatedLane" : "RemoveDedicatedLanes");
            }
            else
            {
                _removeDedicatedLanes.gameObject.SetActive(false);
            }

            _expertPlusOnly.gameObject.SetActive(Item is DrumsHighwayItem.Kick2x or DrumsHighwayItem.Kick2xConditional);
            _expertPlusOnlyText.text = Localize.Key("Menu.HighwayOrdering.ExpertPlusOnly");
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

        
        private static HighwayOrderingElement FOUR_LANE_KICK = new((int) FourLaneDrumPad.Kick, DrumsAction.Kick, (int) FourLaneDrumsFret.Kick);
        private static HighwayOrderingElement FIVE_LANE_KICK = new((int) FiveLaneDrumPad.Kick, DrumsAction.Kick, (int) FiveLaneDrumsFret.Kick);

        private static HighwayOrderingElement FOUR_LANE_DOUBLE_KICK = new(DOUBLE_KICK_FRET_INDEX, DrumsAction.Kick, (int) FourLaneDrumsFret.DoubleKick);
        private static HighwayOrderingElement FIVE_LANE_DOUBLE_KICK = new(DOUBLE_KICK_FRET_INDEX, DrumsAction.Kick, (int) FiveLaneDrumsFret.DoubleKick);

        private static HighwayOrderingElement FOUR_LANE_RED_DRUM = new((int) FourLaneDrumPad.RedDrum, DrumsAction.RedDrum, (int) FourLaneDrumsFret.RedDrum);
        private static HighwayOrderingElement FOUR_LANE_YELLOW_DRUM = new((int) FourLaneDrumPad.YellowDrum, DrumsAction.YellowDrum, (int) FourLaneDrumsFret.YellowDrum);
        private static HighwayOrderingElement FOUR_LANE_BLUE_DRUM = new((int) FourLaneDrumPad.BlueDrum, DrumsAction.BlueDrum, (int) FourLaneDrumsFret.BlueDrum);
        private static HighwayOrderingElement FOUR_LANE_GREEN_DRUM = new((int) FourLaneDrumPad.GreenDrum, DrumsAction.GreenDrum, (int) FourLaneDrumsFret.GreenDrum);
        private static HighwayOrderingElement FOUR_LANE_YELLOW_CYMBAL = new((int) FourLaneDrumPad.YellowCymbal, DrumsAction.YellowCymbal, (int) FourLaneDrumsFret.YellowCymbal);
        private static HighwayOrderingElement FOUR_LANE_BLUE_CYMBAL = new((int) FourLaneDrumPad.BlueCymbal, DrumsAction.BlueCymbal, (int) FourLaneDrumsFret.BlueCymbal);
        private static HighwayOrderingElement FOUR_LANE_GREEN_CYMBAL = new((int) FourLaneDrumPad.GreenCymbal, DrumsAction.GreenCymbal, (int) FourLaneDrumsFret.GreenCymbal);

        private static HighwayOrderingElement FIVE_LANE_RED = new((int) FiveLaneDrumPad.Red, DrumsAction.RedDrum, (int) FiveLaneDrumsFret.Red);
        private static HighwayOrderingElement FIVE_LANE_YELLOW = new((int) FiveLaneDrumPad.Yellow, DrumsAction.YellowCymbal, (int) FiveLaneDrumsFret.Yellow);
        private static HighwayOrderingElement FIVE_LANE_BLUE = new((int) FiveLaneDrumPad.Blue, DrumsAction.BlueDrum, (int) FiveLaneDrumsFret.Blue);
        private static HighwayOrderingElement FIVE_LANE_ORANGE = new((int) FiveLaneDrumPad.Orange, DrumsAction.OrangeCymbal, (int) FiveLaneDrumsFret.Orange);
        private static HighwayOrderingElement FIVE_LANE_GREEN = new((int) FiveLaneDrumPad.Green, DrumsAction.GreenDrum, (int) FiveLaneDrumsFret.Green);


        public static HighwayOrderingInfo GetHighwayOrderingInfo(DrumsHighwayItem item, Instrument instrument)
        {
            return instrument switch {
                Instrument.FourLaneDrums or Instrument.ProDrums => GetFourLaneHighwayOrderingInfo(item),
                Instrument.FiveLaneDrums => GetFiveLaneHighwayOrderingInfo(item),
                _ => throw new ArgumentOutOfRangeException("Unexpected nondrums instrument")
            };
        }

        private static HighwayOrderingInfo GetFourLaneHighwayOrderingInfo(DrumsHighwayItem item)
        {
            return item switch {
                DrumsHighwayItem.Kick               => new(new() { FOUR_LANE_KICK, FOUR_LANE_DOUBLE_KICK }, DrumsBreLaneIndex.Kick ),
                DrumsHighwayItem.Kick1x             => new(new() { FOUR_LANE_KICK }, DrumsBreLaneIndex.Kick),
                DrumsHighwayItem.Kick2x             => new(new() { FOUR_LANE_DOUBLE_KICK }, DrumsBreLaneIndex.Kick ),
                DrumsHighwayItem.Kick2xConditional  => new(new() { FOUR_LANE_DOUBLE_KICK }, DrumsBreLaneIndex.Kick),

                DrumsHighwayItem.Red                => new(new() { FOUR_LANE_RED_DRUM }, DrumsBreLaneIndex.Red),

                DrumsHighwayItem.Yellow             => new(new() { FOUR_LANE_YELLOW_DRUM, FOUR_LANE_YELLOW_CYMBAL }, DrumsBreLaneIndex.Yellow ),
                DrumsHighwayItem.YellowCymbal       => new(new() { FOUR_LANE_YELLOW_CYMBAL }, DrumsBreLaneIndex.YellowCymbal ),
                DrumsHighwayItem.YellowDrum         => new(new() { FOUR_LANE_YELLOW_DRUM }, DrumsBreLaneIndex.YellowDrum ),

                DrumsHighwayItem.Blue               => new(new() { FOUR_LANE_BLUE_DRUM, FOUR_LANE_BLUE_CYMBAL }, DrumsBreLaneIndex.Blue ),
                DrumsHighwayItem.BlueCymbal         => new(new() { FOUR_LANE_BLUE_CYMBAL }, DrumsBreLaneIndex.BlueCymbal ),
                DrumsHighwayItem.BlueDrum           => new(new() { FOUR_LANE_BLUE_DRUM }, DrumsBreLaneIndex.BlueDrum),

                DrumsHighwayItem.Green              => new(new() { FOUR_LANE_GREEN_DRUM, FOUR_LANE_GREEN_CYMBAL }, DrumsBreLaneIndex.Green),
                DrumsHighwayItem.GreenDrum          => new(new() { FOUR_LANE_GREEN_DRUM }, DrumsBreLaneIndex.GreenCymbal),
                DrumsHighwayItem.GreenCymbal        => new(new() { FOUR_LANE_GREEN_CYMBAL }, DrumsBreLaneIndex.GreenDrum),

                _ => new(new() { new((int) item, (DrumsAction) item, (int) item) }, 0)
            };
        }

        private static HighwayOrderingInfo GetFiveLaneHighwayOrderingInfo(DrumsHighwayItem item)
        {
            return item switch {
                DrumsHighwayItem.Kick               => new(new() { FIVE_LANE_KICK, FIVE_LANE_DOUBLE_KICK }, DrumsBreLaneIndex.Kick),
                DrumsHighwayItem.Kick1x             => new(new() { FIVE_LANE_KICK }, DrumsBreLaneIndex.Kick),
                DrumsHighwayItem.Kick2x             => new(new() { FIVE_LANE_DOUBLE_KICK }, DrumsBreLaneIndex.Kick),
                DrumsHighwayItem.Kick2xConditional  => new(new() { FIVE_LANE_DOUBLE_KICK }, DrumsBreLaneIndex.Kick),

                DrumsHighwayItem.Red                => new(new() { FIVE_LANE_RED }, DrumsBreLaneIndex.Red),
                DrumsHighwayItem.Yellow             => new(new() { FIVE_LANE_YELLOW }, DrumsBreLaneIndex.Yellow),
                DrumsHighwayItem.Blue               => new(new() { FIVE_LANE_BLUE }, DrumsBreLaneIndex.Blue),
                DrumsHighwayItem.Orange             => new(new() { FIVE_LANE_ORANGE }, DrumsBreLaneIndex.Orange),
                DrumsHighwayItem.Green              => new(new() { FIVE_LANE_GREEN }, DrumsBreLaneIndex.Green),

                _ => new(new() { new((int) item, (DrumsAction) item, (int) item) }, 0)
            };
        }

        public struct HighwayOrderingInfo
        {
            public HighwayOrderingInfo(List<HighwayOrderingElement> elements, DrumsBreLaneIndex breLaneIndex)
            {
                Elements = elements;
                BreLaneIndex = breLaneIndex;
            }

            // If there are multiple elements (creating a shared fret for multiple note types, like tom+cymbal lanes
            // on Pro Drums), the first element in the list is the one whose color profile settings will be used for
            // the resulting fret
            public List<HighwayOrderingElement> Elements{ get; set; }
            public DrumsBreLaneIndex BreLaneIndex { get; set; }
        }

        public struct HighwayOrderingElement
        {
            public HighwayOrderingElement(int pad, DrumsAction action, int colorIndex)
            {
                Pad = pad;
                Action = action;
                ColorIndex = colorIndex;
            }

            public int Pad { get; set; }
            public DrumsAction Action {get; set;}
            public int ColorIndex {get; set;}
        }
    }
}
