using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Core;
using YARG.Core.Game;
using YARG.Helpers.Extensions;
using YARG.Localization;
using static YARG.Core.Game.ColorProfile;

namespace YARG.Menu.HighwayConfiguration
{


    [DefaultExecutionOrder(-10000)]
    public class DrumsHighwayConfigurationMenu : MonoSingleton<DrumsHighwayConfigurationMenu>
    {
        public Dictionary<DrumsHighwayItem, HighwayOrderingItemSpec> Specs { get; private set; }

        // Workaround to avoid errors when deactivating menu during startup
        private bool _ready;

        public IFretColorProvider ColorProvider { get; private set; }

        [SerializeField]
        private GameObject _ordering;
        [SerializeField]
        private GameObject _kickItem;
        [SerializeField]
        private TextMeshProUGUI _kickText;
        [SerializeField]
        private TextMeshProUGUI _kickDedicatedLaneButtonText;
        [SerializeField]
        private Image _kickImage;
        [SerializeField]
        private TextMeshProUGUI _splitKickWarning;

        [SerializeField]
        private DrumsHighwayItemView _itemPrefab;

        [SerializeField]
        private TextMeshProUGUI _header;

        [SerializeField]
        private Toggle _localLeftyFlipToggle;
        [SerializeField]
        private Toggle _profileMenuLeftyFlipToggle;


        public List<DrumsHighwayItem> HighwayOrdering { get; private set; }
        private List<DrumsHighwayItemView> _itemViews = new();

        public delegate void SetOrdering(List<DrumsHighwayItem> newOrdering);
        SetOrdering _setOrdering;

        public bool Lefty { get; private set; }
        public bool SplitKicksExist { get; private set; }
        public Instrument Instrument { get; private set; }

        private YargProfile _profile;

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
            string header,
            SetOrdering setOrdering,
            Instrument instrument,
            YargProfile profile)
        {
            Specs = specs;
            _profile = profile;
            Instrument = instrument;
            ColorProvider = colorProvider;
            HighwayOrdering = defaultList;

            _header.text = header;
            
            _kickImage.color = colorProvider.GetFretColor((int)FourLaneDrumsFret.Kick).ToUnityColor();
            _kickText.text = Localize.Key("Menu.HighwayOrdering.Kick");
            _kickDedicatedLaneButtonText.text = Localize.Key("Menu.HighwayOrdering.CreateDedicatedLane");

            _itemViews.Clear();
            _ordering.transform.DestroyChildren();

            _setOrdering = setOrdering;

            Lefty = profile.LeftyFlip;
            _ordering.GetComponent<HorizontalLayoutGroup>().reverseArrangement = Lefty;
            _localLeftyFlipToggle.SetIsOnWithoutNotify(Lefty);

            var dedicatedKickExists = false;
            SplitKicksExist = false;

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
                        SplitKicksExist = true;
                    }
                }
            }

            _kickItem.SetActive(!dedicatedKickExists);
            _splitKickWarning.text = Localize.Key("Menu.HighwayOrdering.SplitKicksWarning");
            _splitKickWarning.gameObject.SetActive(SplitKicksExist);

        }

        private void WriteOrderingToProfile()
        {
            _setOrdering(HighwayOrdering);
        }

        public void DecrementItemPosition(DrumsHighwayItem item)
        {
            var index = GetItemIndex(item);
            if (index == 0)
            {
                return;
            }

            MoveItem(item, index, index - 1);
        }

        public void IncrementItemPosition(DrumsHighwayItem item)
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

        public void ToggleLeftyFlip()
        {
            Lefty = !Lefty;
            _profile.LeftyFlip = Lefty;
            _profileMenuLeftyFlipToggle.SetIsOnWithoutNotify(Lefty);
            Initialize(
                Specs,
                ColorProvider,
                HighwayOrdering,
                _header.text,
                _setOrdering,
                Instrument,
                _profile
            );
        }
    }

    public enum DrumsHighwayItemIconType
    {
        Drum,
        Cymbal,
        Combined,
        Kick
    }
}
