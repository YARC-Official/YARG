using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using YARG.Core.Game;
using YARG.Core.Input;
using YARG.Helpers.Extensions;
using YARG.Menu.Navigation;
using YARG.Player;
using YARG.Settings.Customization;

namespace YARG.Menu.HighwayConfiguration
{
    [DefaultExecutionOrder(-10000)]
    public abstract class DrumsHighwayConfigurationMenu<T> : MonoSingleton<DrumsHighwayConfigurationMenu<T>>
    {
        protected abstract Dictionary<T, HighwayOrderingItemSpec<T>> _specs { get; }


        // Workaround to avoid errors when deactivating menu during startup
        private bool _ready;

        protected ColorProfile _colorProfile;

        [SerializeField]
        protected GameObject _ordering;

        [SerializeField]
        protected HighwayOrderingItem<T> _itemPrefab;

        protected List<T> HighwayOrdering = new();

        protected override void SingletonAwake()
        {
            // Match SettingsMenu behavior: initialized at startup, then hidden.
            gameObject.SetActive(false);
            _ready = true;
        }

        public void SetColorProfile(Guid colorProfile)
        {
            _colorProfile = CustomContentManager.ColorProfiles.GetPresetById(colorProfile);
        }

        public void SetOrdering(List<T> newOrdering) {
            HighwayOrdering = newOrdering;
            Populate();
        }

        public void MoveItemLeft(T item)
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

        public void MoveItemRight(T item)
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

        private int GetItemIndex(T item)
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
                    _colorProfile.FourLaneDrums,
                    i is 0,
                    i == HighwayOrdering.Count - 1
                );
            }
        }

    }

    public enum DrumsHighwayItemIconType
    {
        Drum,
        Cymbal,
        Combined,
        Kick
    }

    public enum FiveLaneDrumsHighwayItem
    {
        Red,
        Yellow,
        Blue,
        Orange,
        Green,

        Kick,
        Kick1x,
        Kick2x,
        Kick2xConditional
    }
}
