using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using YARG.Core;
using YARG.Core.Game;
using YARG.Core.Input;
using YARG.Helpers.Extensions;
using YARG.Menu.Navigation;
using YARG.Player;
using YARG.Settings.Customization;
using static UnityEditor.Progress;
using static YARG.Core.Game.ColorProfile;

namespace YARG.Menu.HighwayConfiguration
{
    [DefaultExecutionOrder(-10000)]
    public class DrumsHighwayConfigurationMenu : MonoSingleton<DrumsHighwayConfigurationMenu>
    {
        private const string HEADER_SUFFIX = " Drums Highway Configuration";

        private Dictionary<DrumsHighwayItem, HighwayOrderingItemSpec> _specs { get; set; }

        // Workaround to avoid errors when deactivating menu during startup
        private bool _ready;

        private IFretColorProvider _colorProvider;

        [SerializeField]
        private GameObject _ordering;

        [SerializeField]
        private HighwayOrderingItem _itemPrefab;

        [SerializeField]
        private TextMeshProUGUI _header;

        private List<DrumsHighwayItem> HighwayOrdering { get; set; }

        public delegate void SetOrdering(List<DrumsHighwayItem> newOrdering);
        SetOrdering _setOrdering;

        protected override void SingletonAwake()
        {
            // Match SettingsMenu behavior: initialized at startup, then hidden.
            gameObject.SetActive(false);
            _ready = true;
        }

        public void Initialize(
            Dictionary<DrumsHighwayItem, HighwayOrderingItemSpec> specs,
            IFretColorProvider colorProvider,
            List<DrumsHighwayItem> defaultList,
            string headerPrefix,
            SetOrdering setOrdering
        ) {
            _specs = specs;
            _colorProvider = colorProvider;
            HighwayOrdering = defaultList;
            _header.text = headerPrefix + HEADER_SUFFIX;
            _setOrdering = setOrdering;
            Populate();
        }

        public void MoveItemLeft(DrumsHighwayItem item)
        {
            var index = GetItemIndex(item);
            if (index == 0)
            {
                return;
            }

            HighwayOrdering.RemoveAt(index);
            HighwayOrdering.Insert(index - 1, item);
            Populate();
        }

        public void MoveItemRight(DrumsHighwayItem item)
        {
            var index = GetItemIndex(item);
            if (index == HighwayOrdering.Count - 1)
            {
                return;
            }

            HighwayOrdering.RemoveAt(index);
            HighwayOrdering.Insert(index + 1, item);
            Populate();
        }

        public void MergeItemInto(DrumsHighwayItem source, DrumsHighwayItem target, DrumsHighwayItem merged)
        {
            var sourceIndex = GetItemIndex(source);
            HighwayOrdering.RemoveAt(sourceIndex);

            var targetIndex = GetItemIndex(target);
            HighwayOrdering.RemoveAt(targetIndex);
            HighwayOrdering.Insert(targetIndex, merged);
            Populate();
        }

        public void SplitItemInto(DrumsHighwayItem source, (DrumsHighwayItem, DrumsHighwayItem) split)
        {
            var index = GetItemIndex(source);
            HighwayOrdering.RemoveAt(index);
            HighwayOrdering.Insert(index, split.Item1);
            HighwayOrdering.Insert(index + 1, split.Item2);
            Populate();
        }

        private int GetItemIndex(DrumsHighwayItem item)
        {
            int index = HighwayOrdering.IndexOf(item);
            if (index is -1)
            {
                throw new ArgumentException("Item not found in highway ordering");
            }

            return index;
        }

        protected void Populate()
        {
            _ordering.transform.DestroyChildren();

            for (var i = 0; i < HighwayOrdering.Count; i++)
            {
                var instance = Instantiate(_itemPrefab, _ordering.transform);
                instance.Initialize(
                    this,
                    _specs[HighwayOrdering[i]],
                    _colorProvider,
                    i is 0,
                    i == HighwayOrdering.Count - 1
                );
            }

            _setOrdering(HighwayOrdering);
        }

        public static Dictionary<DrumsHighwayItem, HighwayOrderingItemSpec> FOUR_LANE_SPECS { get; } = new()
        {
            { DrumsHighwayItem.FourLaneRed,     new( "Red",     DrumsHighwayItemIconType.Drum,      (int)FourLaneDrumsFret.RedDrum,     DrumsHighwayItem.FourLaneRed ) },
            { DrumsHighwayItem.FourLaneYellow,  new( "Yellow",  DrumsHighwayItemIconType.Combined,  (int)FourLaneDrumsFret.YellowDrum,  DrumsHighwayItem.FourLaneYellow) },
            { DrumsHighwayItem.FourLaneBlue,    new( "Blue",    DrumsHighwayItemIconType.Combined,  (int)FourLaneDrumsFret.BlueDrum,    DrumsHighwayItem.FourLaneBlue ) },
            { DrumsHighwayItem.FourLaneGreen,   new( "Green",   DrumsHighwayItemIconType.Combined,  (int)FourLaneDrumsFret.GreenDrum,   DrumsHighwayItem.FourLaneGreen) },
        };

        public static Dictionary<DrumsHighwayItem, HighwayOrderingItemSpec> PRO_DRUMS_SPECS { get; } = new()
        {
            { DrumsHighwayItem.FourLaneRed,             new( "Red",     DrumsHighwayItemIconType.Drum,      (int)FourLaneDrumsFret.RedDrum,     DrumsHighwayItem.FourLaneRed ) },

            { DrumsHighwayItem.FourLaneYellow,          new( "Yellow",  DrumsHighwayItemIconType.Combined,  (int)FourLaneDrumsFret.YellowDrum,  DrumsHighwayItem.FourLaneYellow,  (DrumsHighwayItem.FourLaneYellowCymbal, DrumsHighwayItem.FourLaneYellowDrum)) },
            { DrumsHighwayItem.FourLaneBlue,            new( "Blue",    DrumsHighwayItemIconType.Combined,  (int)FourLaneDrumsFret.BlueDrum,    DrumsHighwayItem.FourLaneBlue,    (DrumsHighwayItem.FourLaneBlueCymbal, DrumsHighwayItem.FourLaneBlueDrum)) },
            { DrumsHighwayItem.FourLaneGreen,           new( "Green",   DrumsHighwayItemIconType.Combined,  (int)FourLaneDrumsFret.GreenDrum,   DrumsHighwayItem.FourLaneGreen,   (DrumsHighwayItem.FourLaneGreenCymbal, DrumsHighwayItem.FourLaneGreenDrum)) },

            { DrumsHighwayItem.FourLaneYellowCymbal,    new( "Yellow Cymbal",  DrumsHighwayItemIconType.Cymbal,  (int)FourLaneDrumsFret.YellowCymbal,  DrumsHighwayItem.FourLaneYellowCymbal,   DrumsHighwayItem.FourLaneYellowDrum,  DrumsHighwayItem.FourLaneYellow) },
            { DrumsHighwayItem.FourLaneBlueCymbal,      new( "Blue Cymbal",    DrumsHighwayItemIconType.Cymbal,  (int)FourLaneDrumsFret.BlueCymbal,    DrumsHighwayItem.FourLaneBlueCymbal,     DrumsHighwayItem.FourLaneBlueDrum,    DrumsHighwayItem.FourLaneBlue) },
            { DrumsHighwayItem.FourLaneGreenCymbal,     new( "Green Cymbal",   DrumsHighwayItemIconType.Cymbal,  (int)FourLaneDrumsFret.GreenCymbal,   DrumsHighwayItem.FourLaneGreenCymbal,    DrumsHighwayItem.FourLaneGreenDrum,   DrumsHighwayItem.FourLaneGreen) },

            { DrumsHighwayItem.FourLaneYellowDrum,      new( "Yellow Drum",  DrumsHighwayItemIconType.Drum,  (int)FourLaneDrumsFret.YellowDrum,  DrumsHighwayItem.FourLaneYellowDrum,   DrumsHighwayItem.FourLaneYellowCymbal,   DrumsHighwayItem.FourLaneYellow) },
            { DrumsHighwayItem.FourLaneBlueDrum,        new( "Blue Drum",    DrumsHighwayItemIconType.Drum,  (int)FourLaneDrumsFret.BlueDrum,    DrumsHighwayItem.FourLaneBlueDrum,     DrumsHighwayItem.FourLaneBlueCymbal,     DrumsHighwayItem.FourLaneBlue) },
            { DrumsHighwayItem.FourLaneGreenDrum,       new( "Green Drum",   DrumsHighwayItemIconType.Drum,  (int)FourLaneDrumsFret.GreenDrum,   DrumsHighwayItem.FourLaneGreenDrum,    DrumsHighwayItem.FourLaneGreenCymbal,    DrumsHighwayItem.FourLaneGreen) },
        };

        public static Dictionary<DrumsHighwayItem, HighwayOrderingItemSpec> FIVE_LANE_SPECS { get; } = new()
        {
            { DrumsHighwayItem.FiveLaneRed,     new( "Red",     DrumsHighwayItemIconType.Drum,      (int)FiveLaneDrumsFret.Red,     DrumsHighwayItem.FiveLaneRed ) },
            { DrumsHighwayItem.FiveLaneYellow,  new( "Yellow",  DrumsHighwayItemIconType.Cymbal,    (int)FiveLaneDrumsFret.Yellow,  DrumsHighwayItem.FiveLaneYellow) },
            { DrumsHighwayItem.FiveLaneBlue,    new( "Blue",    DrumsHighwayItemIconType.Drum,      (int)FiveLaneDrumsFret.Blue,    DrumsHighwayItem.FiveLaneBlue ) },
            { DrumsHighwayItem.FiveLaneOrange,  new( "Orange",  DrumsHighwayItemIconType.Cymbal,    (int)FiveLaneDrumsFret.Orange,  DrumsHighwayItem.FiveLaneOrange ) },
            { DrumsHighwayItem.FiveLaneGreen,   new( "Green",   DrumsHighwayItemIconType.Drum,      (int)FiveLaneDrumsFret.Green,   DrumsHighwayItem.FiveLaneGreen) },
        };
    }

    public enum DrumsHighwayItemIconType
    {
        Drum,
        Cymbal,
        Combined,
        Kick
    }
}
