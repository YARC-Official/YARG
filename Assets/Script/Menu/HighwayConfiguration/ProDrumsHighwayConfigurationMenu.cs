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
    public class ProDrumsHighwayConfigurationMenu : DrumsHighwayConfigurationMenu<ProDrumsHighwayItem>
    {
        protected override Dictionary<ProDrumsHighwayItem, HighwayOrderingItemSpec<ProDrumsHighwayItem>> _specs { get; } = new()
        {
            { ProDrumsHighwayItem.Red,     new( "Red",     DrumsHighwayItemIconType.Drum,      (int)FourLaneDrumsFret.RedDrum,     ProDrumsHighwayItem.Red ) },
            { ProDrumsHighwayItem.Yellow,  new( "Yellow",  DrumsHighwayItemIconType.Combined,  (int)FourLaneDrumsFret.YellowDrum,  ProDrumsHighwayItem.Yellow) },
            { ProDrumsHighwayItem.Blue,    new( "Blue",    DrumsHighwayItemIconType.Combined,  (int)FourLaneDrumsFret.BlueDrum,    ProDrumsHighwayItem.Blue ) },
            { ProDrumsHighwayItem.Green,   new( "Green",   DrumsHighwayItemIconType.Combined,  (int)FourLaneDrumsFret.GreenDrum,   ProDrumsHighwayItem.Green) },

            { ProDrumsHighwayItem.YellowCymbal,  new( "Yellow Cymbal",  DrumsHighwayItemIconType.Cymbal,  (int)FourLaneDrumsFret.YellowCymbal,  ProDrumsHighwayItem.YellowCymbal) },
            { ProDrumsHighwayItem.BlueCymbal,    new( "Blue Cymbal",    DrumsHighwayItemIconType.Cymbal,  (int)FourLaneDrumsFret.BlueCymbal,    ProDrumsHighwayItem.BlueCymbal) },
            { ProDrumsHighwayItem.GreenCymbal,   new( "Green Cymbal",   DrumsHighwayItemIconType.Cymbal,  (int)FourLaneDrumsFret.GreenCymbal,   ProDrumsHighwayItem.GreenCymbal) },

            { ProDrumsHighwayItem.YellowTom,  new( "Yellow Drum",  DrumsHighwayItemIconType.Drum,  (int)FourLaneDrumsFret.YellowDrum,  ProDrumsHighwayItem.YellowTom) },
            { ProDrumsHighwayItem.BlueTom,    new( "Blue Drum",    DrumsHighwayItemIconType.Drum,  (int)FourLaneDrumsFret.BlueDrum,    ProDrumsHighwayItem.BlueTom) },
            { ProDrumsHighwayItem.GreenTom,   new( "Green Drum",   DrumsHighwayItemIconType.Drum,  (int)FourLaneDrumsFret.GreenDrum,   ProDrumsHighwayItem.GreenTom) },
        };
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

}
