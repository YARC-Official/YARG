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
        protected override Dictionary<FourLaneDrumsHighwayItem, HighwayOrderingItemSpec<FourLaneDrumsHighwayItem>> _specs { get; } = new()
        {
            { FourLaneDrumsHighwayItem.Red,     new( "Red",     DrumsHighwayItemIconType.Drum,      (int)FourLaneDrumsFret.RedDrum,     FourLaneDrumsHighwayItem.Red ) },
            { FourLaneDrumsHighwayItem.Yellow,  new( "Yellow",  DrumsHighwayItemIconType.Combined,  (int)FourLaneDrumsFret.YellowDrum,  FourLaneDrumsHighwayItem.Yellow) },
            { FourLaneDrumsHighwayItem.Blue,    new( "Blue",    DrumsHighwayItemIconType.Combined,  (int)FourLaneDrumsFret.BlueDrum,    FourLaneDrumsHighwayItem.Blue ) },
            { FourLaneDrumsHighwayItem.Green,   new( "Green",   DrumsHighwayItemIconType.Combined,  (int)FourLaneDrumsFret.GreenDrum,   FourLaneDrumsHighwayItem.Green) },
        };
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
