using System.Collections.Generic;
using YARG.Data;

namespace YARG.Serialization
{
    public static class DtaDifficulty
    {
        private static readonly Dictionary<Instrument, int[]> DiffMaps = new()
        {
            {
                Instrument.GUITAR, new int[]
                {
                    139, 176, 221, 267, 333, 409
                }
            },
            {
                Instrument.BASS, new int[]
                {
                    135, 181, 228, 293, 364, 436
                }
            },
            {
                Instrument.DRUMS, new int[]
                {
                    124, 151, 178, 242, 345, 448
                }
            },
            {
                Instrument.KEYS, new int[]
                {
                    153, 211, 269, 327, 385, 443
                }
            },
            {
                Instrument.VOCALS, new int[]
                {
                    132, 175, 218, 279, 353, 427
                }
            },
            {
                Instrument.REAL_GUITAR, new int[]
                {
                    150, 205, 264, 323, 382, 442
                }
            },
            {
                Instrument.REAL_BASS, new int[]
                {
                    150, 208, 267, 325, 384, 442
                }
            },
            {
                Instrument.REAL_DRUMS, new int[]
                {
                    124, 151, 178, 242, 345, 448
                }
            },
            {
                Instrument.REAL_KEYS, new int[]
                {
                    153, 211, 269, 327, 385, 443
                }
            },
            {
                Instrument.HARMONY, new int[]
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