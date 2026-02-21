using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YARG.Core.Chart;
using YARG.Menu.HighwayConfiguration;
using static YARG.Core.Game.ColorProfile;

namespace YARG.Menu.HighwayConfiguration
{
    public class FourLaneDrumsHighwayConfigurationMenu : DrumsHighwayConfigurationMenu<FourLaneDrumsHighwayItem>
    {
        private static Dictionary<FourLaneDrumsHighwayItem, HighwayOrderingItemSpec> specs = new()
        {
            { FourLaneDrumsHighwayItem.Red, new( "Red", DrumsHighwayItemIconType.Drum, (int)FourLaneDrumsFret.RedDrum ) },
            { FourLaneDrumsHighwayItem.Yellow, new( "Yellow", DrumsHighwayItemIconType.Combined, (int)FourLaneDrumsFret.YellowDrum ) },
            { FourLaneDrumsHighwayItem.Blue, new( "Blue", DrumsHighwayItemIconType.Combined, (int)FourLaneDrumsFret.BlueDrum ) },
            { FourLaneDrumsHighwayItem.Green, new( "Green", DrumsHighwayItemIconType.Combined, (int)FourLaneDrumsFret.GreenDrum ) },
        };


        protected override void Populate()
        {
            for (var i = 0; i < HighwayOrdering.Count; i++)
            {
                var instance = Instantiate(_itemPrefab, _ordering.transform);
                instance.Initialize(
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
