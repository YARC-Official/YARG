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
    public class FiveLaneDrumsHighwayConfigurationMenu : DrumsHighwayConfigurationMenu<FiveLaneDrumsHighwayItem>
    {
        protected override Dictionary<FiveLaneDrumsHighwayItem, HighwayOrderingItemSpec<FiveLaneDrumsHighwayItem>> _specs { get; } = new()
        {
            { FiveLaneDrumsHighwayItem.Red,     new( "Red",     DrumsHighwayItemIconType.Drum,      (int)FiveLaneDrumsFret.Red,     FiveLaneDrumsHighwayItem.Red ) },
            { FiveLaneDrumsHighwayItem.Yellow,  new( "Yellow",  DrumsHighwayItemIconType.Cymbal,    (int)FiveLaneDrumsFret.Yellow,  FiveLaneDrumsHighwayItem.Yellow) },
            { FiveLaneDrumsHighwayItem.Blue,    new( "Blue",    DrumsHighwayItemIconType.Drum,      (int)FiveLaneDrumsFret.Blue,    FiveLaneDrumsHighwayItem.Blue ) },
            { FiveLaneDrumsHighwayItem.Orange,  new( "Orange",  DrumsHighwayItemIconType.Cymbal,    (int)FiveLaneDrumsFret.Orange,  FiveLaneDrumsHighwayItem.Orange ) },
            { FiveLaneDrumsHighwayItem.Green,   new( "Green",   DrumsHighwayItemIconType.Drum,      (int)FiveLaneDrumsFret.Green,   FiveLaneDrumsHighwayItem.Green) },
        };
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
