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

            { ProDrumsHighwayItem.Yellow,  new( "Yellow",  DrumsHighwayItemIconType.Combined,  (int)FourLaneDrumsFret.YellowDrum,  ProDrumsHighwayItem.Yellow,  (ProDrumsHighwayItem.YellowCymbal, ProDrumsHighwayItem.YellowTom)) },
            { ProDrumsHighwayItem.Blue,    new( "Blue",    DrumsHighwayItemIconType.Combined,  (int)FourLaneDrumsFret.BlueDrum,    ProDrumsHighwayItem.Blue,    (ProDrumsHighwayItem.BlueCymbal, ProDrumsHighwayItem.BlueTom) ) },
            { ProDrumsHighwayItem.Green,   new( "Green",   DrumsHighwayItemIconType.Combined,  (int)FourLaneDrumsFret.GreenDrum,   ProDrumsHighwayItem.Green,   (ProDrumsHighwayItem.GreenCymbal, ProDrumsHighwayItem.GreenTom)) },

            { ProDrumsHighwayItem.YellowCymbal,  new( "Yellow Cymbal",  DrumsHighwayItemIconType.Cymbal,  (int)FourLaneDrumsFret.YellowCymbal,  ProDrumsHighwayItem.YellowCymbal,   ProDrumsHighwayItem.YellowTom,  ProDrumsHighwayItem.Yellow) },
            { ProDrumsHighwayItem.BlueCymbal,    new( "Blue Cymbal",    DrumsHighwayItemIconType.Cymbal,  (int)FourLaneDrumsFret.BlueCymbal,    ProDrumsHighwayItem.BlueCymbal,     ProDrumsHighwayItem.BlueTom,    ProDrumsHighwayItem.Blue) },
            { ProDrumsHighwayItem.GreenCymbal,   new( "Green Cymbal",   DrumsHighwayItemIconType.Cymbal,  (int)FourLaneDrumsFret.GreenCymbal,   ProDrumsHighwayItem.GreenCymbal,    ProDrumsHighwayItem.GreenTom,   ProDrumsHighwayItem.Green) },

            { ProDrumsHighwayItem.YellowTom,  new( "Yellow Drum",  DrumsHighwayItemIconType.Drum,  (int)FourLaneDrumsFret.YellowDrum,  ProDrumsHighwayItem.YellowTom,   ProDrumsHighwayItem.YellowCymbal,   ProDrumsHighwayItem.Yellow) },
            { ProDrumsHighwayItem.BlueTom,    new( "Blue Drum",    DrumsHighwayItemIconType.Drum,  (int)FourLaneDrumsFret.BlueDrum,    ProDrumsHighwayItem.BlueTom,     ProDrumsHighwayItem.BlueCymbal,     ProDrumsHighwayItem.Blue) },
            { ProDrumsHighwayItem.GreenTom,   new( "Green Drum",   DrumsHighwayItemIconType.Drum,  (int)FourLaneDrumsFret.GreenDrum,   ProDrumsHighwayItem.GreenTom,    ProDrumsHighwayItem.GreenCymbal,    ProDrumsHighwayItem.Green) },
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
