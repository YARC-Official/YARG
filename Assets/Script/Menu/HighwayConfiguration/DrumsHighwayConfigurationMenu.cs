using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using YARG.Core.Game;
using YARG.Core.Input;
using YARG.Menu.Navigation;
using YARG.Player;
using YARG.Settings.Customization;

namespace YARG.Menu.HighwayConfiguration
{
    [DefaultExecutionOrder(-10000)]
    public abstract class DrumsHighwayConfigurationMenu<T> : MonoSingleton<DrumsHighwayConfigurationMenu<T>>
    {
        // Workaround to avoid errors when deactivating menu during startup
        private bool _ready;

        protected ColorProfile _colorProfile;

        [SerializeField]
        protected GameObject _ordering;

        [SerializeField]
        protected HighwayOrderingItem _itemPrefab;

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

        protected abstract void Populate();
        
    }

    public enum DrumsHighwayItemIconType
    {
        Drum,
        Cymbal,
        Combined,
        Kick
    }

    public enum ProDrumsHighwayItem
    {
        Red,
        Yellow,
        Blue,
        Green,

        YellowCymbal,
        YellowTom,
        BlueCymbal,
        BlueTom,
        GreenCymbal,
        GreenTom,

        Kick,
        Kick1x,
        Kick2x,
        Kick2xConditional
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
