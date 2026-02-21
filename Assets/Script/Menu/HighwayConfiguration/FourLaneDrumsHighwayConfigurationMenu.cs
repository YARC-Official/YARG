using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YARG.Core.Chart;
using YARG.Helpers.Extensions;
using YARG.Menu.HighwayConfiguration;
using static YARG.Core.Game.ColorProfile;

namespace YARG.Menu.HighwayConfiguration
{
    public class FourLaneDrumsHighwayConfigurationMenu : DrumsHighwayConfigurationMenu<FourLaneDrumsHighwayItem>
    {
        private static Dictionary<FourLaneDrumsHighwayItem, HighwayOrderingItemSpec<FourLaneDrumsHighwayItem>> specs = new()
        {
            { FourLaneDrumsHighwayItem.Red,     new( "Red",     DrumsHighwayItemIconType.Drum,      (int)FourLaneDrumsFret.RedDrum,     FourLaneDrumsHighwayItem.Red ) },
            { FourLaneDrumsHighwayItem.Yellow,  new( "Yellow",  DrumsHighwayItemIconType.Combined,  (int)FourLaneDrumsFret.YellowDrum,  FourLaneDrumsHighwayItem.Yellow) },
            { FourLaneDrumsHighwayItem.Blue,    new( "Blue",    DrumsHighwayItemIconType.Combined,  (int)FourLaneDrumsFret.BlueDrum,    FourLaneDrumsHighwayItem.Blue ) },
            { FourLaneDrumsHighwayItem.Green,   new( "Green",   DrumsHighwayItemIconType.Combined,  (int)FourLaneDrumsFret.GreenDrum,   FourLaneDrumsHighwayItem.Green) },
        };


        protected override void Populate()
        {
            _ordering.transform.DestroyChildren();

            for (var i = 0; i < HighwayOrdering.Count; i++)
            {
                var instance = Instantiate(_itemPrefab, _ordering.transform);
                instance.Initialize(
                    this,
                    specs[HighwayOrdering[i]],
                    _colorProfile.FourLaneDrums,
                    i is 0,
                    i == HighwayOrdering.Count - 1
                );
            }
        }
    }

    public enum FourLaneDrumsHighwayItem
    {
        Red,
        Yellow,
        Blue,
        Green,

        Kick,
        Kick1x,
        Kick2x,
        Kick2xConditional
    }
}
