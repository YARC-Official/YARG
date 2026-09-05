using System;
using System.Collections.Generic;
using System.Text;
using YARG.Core;
using YARG.Core.Chart;

namespace YARG.Helpers
{
    public static class GryboHighwayHelpers
    {
        public static bool ShouldUseOpenLane(OpenLaneDisplayType displayType, List<GuitarNote> notes)
        {
            switch (displayType)
            {
                case OpenLaneDisplayType.Never:
                    return false;
                case OpenLaneDisplayType.Always:
                    return true;
                case OpenLaneDisplayType.IfChartContainsOpens:
                    foreach (var note in notes)
                    {
                        foreach (var child in note.AllNotes)
                        {
                            if (child.Fret is (int) FiveFretGuitarFret.Open)
                            {
                                return true;
                            }
                        }
                    }
                    return false;
                default:
                    throw new ArgumentOutOfRangeException("Unrecognized OpenLaneDisplayType");
            }
        }

        public static Dictionary<int, int> DEFAULT_HIGHWAY_ORDERING = new()
        {
            { (int)FiveFretGuitarFret.Green,     0 },
            { (int)FiveFretGuitarFret.Red,       1 },
            { (int)FiveFretGuitarFret.Yellow,    2 },
            { (int)FiveFretGuitarFret.Blue,      3 },
            { (int)FiveFretGuitarFret.Orange,    4 },
        };

        public static Dictionary<int, int> LEFTY_HIGHWAY_ORDERING = new()
        {
            { (int)FiveFretGuitarFret.Orange,    0 },
            { (int)FiveFretGuitarFret.Blue,      1 },
            { (int)FiveFretGuitarFret.Yellow,    2 },
            { (int)FiveFretGuitarFret.Red,       3 },
            { (int)FiveFretGuitarFret.Green,     4 },
        };

        public static Dictionary<int, int> OPEN_LANE_HIGHWAY_ORDERING = new()
        {
            { (int) FiveFretGuitarFret.Open,    0 },
            { (int) FiveFretGuitarFret.Green,   1 },
            { (int) FiveFretGuitarFret.Red,     2 },
            { (int) FiveFretGuitarFret.Yellow,  3 },
            { (int) FiveFretGuitarFret.Blue,    4 },
            { (int) FiveFretGuitarFret.Orange,  5 },
        };

        public static Dictionary<int, int> OPEN_LANE_LEFTY_HIGHWAY_ORDERING = new()
        {
            { (int) FiveFretGuitarFret.Orange,  0 },
            { (int) FiveFretGuitarFret.Blue,    1 },
            { (int) FiveFretGuitarFret.Yellow,  2 },
            { (int) FiveFretGuitarFret.Red,     3 },
            { (int) FiveFretGuitarFret.Green,   4 },
            { (int) FiveFretGuitarFret.Open,    5 },
        };
    }
}
