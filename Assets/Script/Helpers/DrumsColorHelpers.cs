using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YARG.Core;
using static YARG.Core.Game.ColorProfile;

namespace YARG.Helpers
{
    public static class DrumsColorHelpers
    {
        public static int ApplyHandednessToFourLaneColor(FourLaneDrumsFret fret, bool lefty, bool splitKicks)
        {
            if (lefty)
            {
                return fret switch
                {
                    FourLaneDrumsFret.RedDrum => (int) FourLaneDrumsFret.GreenDrum,
                    FourLaneDrumsFret.YellowDrum => (int) FourLaneDrumsFret.BlueDrum,
                    FourLaneDrumsFret.BlueDrum => (int) FourLaneDrumsFret.YellowDrum,
                    FourLaneDrumsFret.GreenDrum => (int) FourLaneDrumsFret.RedDrum,
                    FourLaneDrumsFret.YellowCymbal => (int) FourLaneDrumsFret.BlueCymbal,
                    FourLaneDrumsFret.BlueCymbal => (int) FourLaneDrumsFret.YellowCymbal,
                    FourLaneDrumsFret.GreenCymbal => (int) FourLaneDrumsFret.RedCymbal,
                    _ => (int) fret
                };
            }

            return (int) fret;
        }

        public static int ApplyHandednessToFiveLaneColor(FiveLaneDrumsFret fret, bool lefty, bool splitKicks)
        {
            if (lefty)
            {
                return fret switch
                {
                    FiveLaneDrumsFret.Red => (int) FiveLaneDrumsFret.Green,
                    FiveLaneDrumsFret.Yellow => (int) FiveLaneDrumsFret.Orange,
                    FiveLaneDrumsFret.Blue => (int) FiveLaneDrumsFret.Blue,
                    FiveLaneDrumsFret.Orange => (int) FiveLaneDrumsFret.Yellow,
                    FiveLaneDrumsFret.Green => (int) FiveLaneDrumsFret.Red,
                    _ => (int) fret
                };
            }

            return (int) fret;
        }

        public static int ApplyHandednessToColor(int fret, bool lefty, bool splitKicks, Instrument instrument)
        {
            return instrument switch
            {
                Instrument.FourLaneDrums or Instrument.ProDrums => ApplyHandednessToFourLaneColor((FourLaneDrumsFret) fret, lefty, splitKicks),
                Instrument.FiveLaneDrums => ApplyHandednessToFiveLaneColor((FiveLaneDrumsFret) fret, lefty, splitKicks),
                _ => throw new ArgumentOutOfRangeException("Unexpected nondrums instrument")
            };
        }
    }
}
