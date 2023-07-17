using System.Collections.Generic;
using YARG.Core;

namespace YARG.Serialization
{
    public static class DtaDifficulty
    {
        private static readonly Dictionary<Instrument, int[]> DiffMaps = new()
        {
            {
                Instrument.FiveFretGuitar, new int[]
                {
                    139, 176, 221, 267, 333, 409
                }
            },
            {
                Instrument.FiveFretBass, new int[]
                {
                    135, 181, 228, 293, 364, 436
                }
            },
            {
                Instrument.FourLaneDrums, new int[]
                {
                    124, 151, 178, 242, 345, 448
                }
            },
            {
                Instrument.Keys, new int[]
                {
                    153, 211, 269, 327, 385, 443
                }
            },
            {
                Instrument.Vocals, new int[]
                {
                    132, 175, 218, 279, 353, 427
                }
            },
            {
                Instrument.ProGuitar_17Fret, new int[]
                {
                    150, 205, 264, 323, 382, 442
                }
            },
            {
                Instrument.ProBass_17Fret, new int[]
                {
                    150, 208, 267, 325, 384, 442
                }
            },
            {
                Instrument.ProDrums, new int[]
                {
                    124, 151, 178, 242, 345, 448
                }
            },
            // {
            //     Instrument.ProKeys, new int[]
            //     {
            //         153, 211, 269, 327, 385, 443
            //     }
            // },
            {
                Instrument.Harmony, new int[]
                {
                    132, 175, 218, 279, 353, 427
                }
            },
        };

        private static readonly int[] BandDiffMap =
        {
            163, 215, 243, 267, 292, 345
        };

        public static int ToNumberedDiff(Instrument instrument, int dtaDiff)
        {
            if (dtaDiff == 0)
            {
                return -1;
            }

            var map = DiffMaps[instrument];

            for (int i = map.Length - 1; i > 0; i--)
            {
                if (dtaDiff > map[i])
                {
                    return i + 1;
                }
            }

            return 0;
        }

        public static int ToNumberedDiffForBand(int dtaDiff)
        {
            if (dtaDiff == 0)
            {
                return -1;
            }

            for (int i = BandDiffMap.Length - 1; i > 0; i--)
            {
                if (dtaDiff > BandDiffMap[i])
                {
                    return i + 1;
                }
            }

            return 0;
        }
    }
}