using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEditor.Rendering.LookDev;
using UnityEngine;
using UnityEngine.UI;
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

        public Dictionary<DrumsHighwayItem, HighwayOrderingItemSpec> Specs { get; private set; }

        // Workaround to avoid errors when deactivating menu during startup
        private bool _ready;

        public IFretColorProvider ColorProvider { get; private set; }

        [SerializeField]
        private GameObject _ordering;
        [SerializeField]
        private GameObject _kickItem;
        [SerializeField]
        private Image _kickImage;
        [SerializeField]
        private TextMeshProUGUI _splitKickWarning;

        [SerializeField]
        private DrumsHighwayItemView _itemPrefab;

        [SerializeField]
        private TextMeshProUGUI _header;

        public List<DrumsHighwayItem> HighwayOrdering { get; private set; }
        private List<DrumsHighwayItemView> _itemViews = new();

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
            Specs = specs;
            ColorProvider = colorProvider;
            HighwayOrdering = defaultList;
            _header.text = headerPrefix + HEADER_SUFFIX;
            
            _kickImage.color = colorProvider.GetFretColor((int)FourLaneDrumsFret.Kick).ToUnityColor();

            _itemViews.Clear();
            _ordering.transform.DestroyChildren();

            _setOrdering = setOrdering;

            var dedicatedKickExists = false;
            var splitKicksExist = false;

            foreach (var item in HighwayOrdering)
            {
                var view = Instantiate(_itemPrefab, _ordering.transform);
                view.Initialize(this, item);
                _itemViews.Add(view);

                if (item.IsKick())
                {
                    dedicatedKickExists = true;
                    if (item is DrumsHighwayItem.Kick1x)
                    {
                        splitKicksExist = true;
                    }
                }
            }

            _kickItem.SetActive(!dedicatedKickExists);
            _splitKickWarning.gameObject.SetActive(splitKicksExist);
        }

        private void WriteOrderingToProfile()
        {
            _setOrdering(HighwayOrdering);
        }

        public void MoveItemLeft(DrumsHighwayItem item)
        {
            var index = GetItemIndex(item);
            if (index == 0)
            {
                return;
            }

            MoveItem(item, index, index - 1);
        }

        public void MoveItemRight(DrumsHighwayItem item)
        {
            var index = GetItemIndex(item);
            if (index == HighwayOrdering.Count - 1)
            {
                return;
            }

            MoveItem(item, index, index + 1);
        }

        private void MoveItem(DrumsHighwayItem item, int oldIndex, int newIndex)
        {
            HighwayOrdering.RemoveAt(oldIndex);
            HighwayOrdering.Insert(newIndex, item);

            var view = _itemViews[oldIndex];
            var swappee = _itemViews[newIndex];

            _itemViews.RemoveAt(oldIndex);
            _itemViews.Insert(newIndex, view);

            view.transform.SetSiblingIndex(newIndex);
            view.Render();
            swappee.Render();

            WriteOrderingToProfile();
        }

        public void MergeItemInto(DrumsHighwayItem source, DrumsHighwayItem target, DrumsHighwayItem merged)
        {
            var sourceIndex = GetItemIndex(source);
            HighwayOrdering.RemoveAt(sourceIndex);
            var sourceView = _itemViews[sourceIndex];
            _itemViews.RemoveAt(sourceIndex);
            Destroy(sourceView.gameObject);


            var targetIndex = GetItemIndex(target);
            HighwayOrdering[targetIndex] = merged;
            var targetView = _itemViews[targetIndex];
            targetView.Initialize(this, merged);
            WriteOrderingToProfile();

            if (merged is DrumsHighwayItem.Kick)
            {
                _splitKickWarning.gameObject.SetActive(false);
            }
        }

        public void SplitItemInto(DrumsHighwayItem source, (DrumsHighwayItem, DrumsHighwayItem) split)
        {
            var index1 = GetItemIndex(source);
            var index2 = index1 + 1;

            HighwayOrdering[index1] = split.Item1;
            HighwayOrdering.Insert(index2, split.Item2);

            var view1 = _itemViews[index1];
            view1.Initialize(this, split.Item1);

            var view2 = Instantiate(_itemPrefab, _ordering.transform);
            view2.Initialize(this, split.Item2);
            view2.transform.SetSiblingIndex(index2);
            _itemViews.Insert(index2, view2);
            WriteOrderingToProfile();

            if (source is DrumsHighwayItem.Kick)
            {
                _splitKickWarning.gameObject.SetActive(true);
            }
        }

        public void CreateDedicatedKickLane()
        {
            var midpoint = HighwayOrdering.Count / 2;
            HighwayOrdering.Insert(midpoint, DrumsHighwayItem.Kick);

            var view = Instantiate(_itemPrefab, _ordering.transform);
            view.Initialize(this, DrumsHighwayItem.Kick);
            view.transform.SetSiblingIndex(midpoint);

            _itemViews.Insert(midpoint, view);

            _kickItem.gameObject.SetActive(false);
            WriteOrderingToProfile();
        }

        public void RemoveDedicatedKickLanes()
        {
            for (var i = HighwayOrdering.Count - 1; i >= 0; i--)
            {
                if (HighwayOrdering[i].IsKick())
                {
                    HighwayOrdering.RemoveAt(i);
                    Destroy(_itemViews[i].gameObject);
                    _itemViews.RemoveAt(i);
                }
            }

            _itemViews.First().Render();
            _itemViews.Last().Render();

            _kickItem.SetActive(true);
            _splitKickWarning.gameObject.SetActive(false);
            WriteOrderingToProfile();
        }

        public void ToggleExpertPlusOnly(DrumsHighwayItemView caller)
        {
            var oldItem = caller.Item;
            var newItem = caller.Item switch
            {
                DrumsHighwayItem.Kick2x => DrumsHighwayItem.Kick2xConditional,
                DrumsHighwayItem.Kick2xConditional => DrumsHighwayItem.Kick2x,
                _ => throw new ArgumentOutOfRangeException("Attempted to toggle Expert+ Only on something other than a 2x Kick")
            };

            HighwayOrdering[HighwayOrdering.IndexOf(oldItem)] = newItem;
            
            caller.Item = newItem;
            caller.Render();
            WriteOrderingToProfile();
        }

        public int GetItemIndex(DrumsHighwayItem item)
        {
            var index = HighwayOrdering.IndexOf(item);
            if (index is -1)
            {
                throw new ArgumentException("Item not found in highway ordering");
            }

            return index;
        }

        public static Dictionary<DrumsHighwayItem, HighwayOrderingItemSpec> FOUR_LANE_SPECS { get; } = new()
        {
            { DrumsHighwayItem.Kick, new( "Kick", DrumsHighwayItemIconType.Kick, (int)FourLaneDrumsFret.Kick, DrumsHighwayItem.Kick, (DrumsHighwayItem.Kick2x, DrumsHighwayItem.Kick1x)) },
            { DrumsHighwayItem.Kick1x, new( "Right Kick", DrumsHighwayItemIconType.Kick, (int)FourLaneDrumsFret.Kick, DrumsHighwayItem.Kick1x, DrumsHighwayItem.Kick2x, DrumsHighwayItem.Kick) },
            { DrumsHighwayItem.Kick2x, new( "Left Kick", DrumsHighwayItemIconType.Kick, (int)FourLaneDrumsFret.DoubleKick, DrumsHighwayItem.Kick2x, DrumsHighwayItem.Kick1x, DrumsHighwayItem.Kick) },
            { DrumsHighwayItem.Kick2xConditional, new( "Left Kick", DrumsHighwayItemIconType.Kick, (int)FourLaneDrumsFret.DoubleKick, DrumsHighwayItem.Kick2x, DrumsHighwayItem.Kick1x, DrumsHighwayItem.Kick) },

            { DrumsHighwayItem.FourLaneRed,     new( "Red",     DrumsHighwayItemIconType.Drum,      (int)FourLaneDrumsFret.RedDrum,     DrumsHighwayItem.FourLaneRed ) },
            { DrumsHighwayItem.FourLaneYellow,  new( "Yellow",  DrumsHighwayItemIconType.Combined,  (int)FourLaneDrumsFret.YellowDrum,  DrumsHighwayItem.FourLaneYellow) },
            { DrumsHighwayItem.FourLaneBlue,    new( "Blue",    DrumsHighwayItemIconType.Combined,  (int)FourLaneDrumsFret.BlueDrum,    DrumsHighwayItem.FourLaneBlue ) },
            { DrumsHighwayItem.FourLaneGreen,   new( "Green",   DrumsHighwayItemIconType.Combined,  (int)FourLaneDrumsFret.GreenDrum,   DrumsHighwayItem.FourLaneGreen) },
        };

        public static Dictionary<DrumsHighwayItem, HighwayOrderingItemSpec> PRO_DRUMS_SPECS { get; } = new()
        {
            { DrumsHighwayItem.Kick,    new( "Kick",        DrumsHighwayItemIconType.Kick, (int)FourLaneDrumsFret.Kick,         DrumsHighwayItem.Kick,      (DrumsHighwayItem.Kick2x, DrumsHighwayItem.Kick1x)) },
            { DrumsHighwayItem.Kick1x,  new( "Right Kick*",  DrumsHighwayItemIconType.Kick, (int)FourLaneDrumsFret.Kick,         DrumsHighwayItem.Kick1x,    DrumsHighwayItem.Kick2x, DrumsHighwayItem.Kick) },
            { DrumsHighwayItem.Kick2x,  new( "Left Kick*",   DrumsHighwayItemIconType.Kick, (int)FourLaneDrumsFret.DoubleKick,   DrumsHighwayItem.Kick2x,    DrumsHighwayItem.Kick1x, DrumsHighwayItem.Kick) },
            { DrumsHighwayItem.Kick2xConditional,  new( "Left Kick*",   DrumsHighwayItemIconType.Kick, (int)FourLaneDrumsFret.DoubleKick,   DrumsHighwayItem.Kick2x,    DrumsHighwayItem.Kick1x, DrumsHighwayItem.Kick) },

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
            { DrumsHighwayItem.Kick, new( "Kick", DrumsHighwayItemIconType.Kick, (int)FiveLaneDrumsFret.Kick, DrumsHighwayItem.Kick, (DrumsHighwayItem.Kick2x, DrumsHighwayItem.Kick1x)) },
            { DrumsHighwayItem.Kick1x, new( "Right Kick", DrumsHighwayItemIconType.Kick, (int)FiveLaneDrumsFret.Kick, DrumsHighwayItem.Kick1x, DrumsHighwayItem.Kick2x, DrumsHighwayItem.Kick) },
            { DrumsHighwayItem.Kick2x, new( "Left Kick", DrumsHighwayItemIconType.Kick, (int)FiveLaneDrumsFret.DoubleKick, DrumsHighwayItem.Kick2x, DrumsHighwayItem.Kick1x, DrumsHighwayItem.Kick) },
            { DrumsHighwayItem.Kick2xConditional, new( "Left Kick", DrumsHighwayItemIconType.Kick, (int)FiveLaneDrumsFret.DoubleKick, DrumsHighwayItem.Kick2x, DrumsHighwayItem.Kick1x, DrumsHighwayItem.Kick) },

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
