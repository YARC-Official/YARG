using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YARG.Core;
using static YARG.Core.Game.ColorProfile;

namespace YARG.Menu.HighwayConfiguration
{
    public static class DrumsHighwaySpecs
    {
        public static Dictionary<DrumsHighwayItem, HighwayOrderingItemSpec> FOUR_LANE_SPECS { get; } = new()
        {
            { DrumsHighwayItem.Kick, new() {
                Name = "Kick",
                LeftyName = "Kick",
                Type = DrumsHighwayItemIconType.Kick,
                ColorIndex = (int)FourLaneDrumsFret.Kick,
                Value = DrumsHighwayItem.Kick,
                SplitsInto = (DrumsHighwayItem.Kick2x, DrumsHighwayItem.Kick1x)
            } },

            { DrumsHighwayItem.Kick1x, new() {
                Name = "Right Kick*",
                LeftyName = "Left Kick*",
                Type = DrumsHighwayItemIconType.Kick,
                ColorIndex = (int)FourLaneDrumsFret.Kick,
                Value = DrumsHighwayItem.Kick1x,
                MergesInto = DrumsHighwayItem.Kick2x,
                MergedResult = DrumsHighwayItem.Kick
            } },

            { DrumsHighwayItem.Kick2x, new() {
                Name = "Left Kick*",
                LeftyName = "Right Kick*",
                Type = DrumsHighwayItemIconType.Kick,
                ColorIndex = (int)FourLaneDrumsFret.DoubleKick,
                Value = DrumsHighwayItem.Kick2x,
                MergesInto = DrumsHighwayItem.Kick1x,
                MergedResult = DrumsHighwayItem.Kick
            } },

            { DrumsHighwayItem.Kick2xConditional, new() {
                Name = "Left Kick*",
                LeftyName = "Right Kick*",
                Type = DrumsHighwayItemIconType.Kick,
                ColorIndex = (int)FourLaneDrumsFret.DoubleKick,
                Value = DrumsHighwayItem.Kick2x,
                MergesInto = DrumsHighwayItem.Kick1x,
                MergedResult = DrumsHighwayItem.Kick
            } },

            { DrumsHighwayItem.FourLaneRed, new() {
                Name = "Red",
                LeftyName = "Green",
                Type = DrumsHighwayItemIconType.Drum,
                ColorIndex = (int)FourLaneDrumsFret.RedDrum,
                Value = DrumsHighwayItem.FourLaneRed
            } },

            { DrumsHighwayItem.FourLaneYellow, new() {
                Name = "Yellow",
                LeftyName = "Blue",
                Type = DrumsHighwayItemIconType.Combined,
                ColorIndex = (int)FourLaneDrumsFret.YellowDrum,
                Value = DrumsHighwayItem.FourLaneYellow
            } },

            { DrumsHighwayItem.FourLaneBlue, new() {
                Name = "Blue",
                LeftyName = "Yellow",
                Type = DrumsHighwayItemIconType.Combined,
                ColorIndex = (int)FourLaneDrumsFret.BlueDrum,
                Value = DrumsHighwayItem.FourLaneBlue
            } },

            { DrumsHighwayItem.FourLaneGreen, new() {
                Name = "Green",
                LeftyName = "Red",
                Type = DrumsHighwayItemIconType.Combined,
                ColorIndex = (int)FourLaneDrumsFret.GreenDrum,
                Value = DrumsHighwayItem.FourLaneGreen
            } },
        };

        public static Dictionary<DrumsHighwayItem, HighwayOrderingItemSpec> PRO_DRUMS_SPECS { get; } = new()
        {
           { DrumsHighwayItem.Kick, new() {
                Name = "Kick",
                LeftyName = "Kick",
                Type = DrumsHighwayItemIconType.Kick,
                ColorIndex = (int)FourLaneDrumsFret.Kick,
                Value = DrumsHighwayItem.Kick,
                SplitsInto = (DrumsHighwayItem.Kick2x, DrumsHighwayItem.Kick1x)
            } },

            { DrumsHighwayItem.Kick1x, new() {
                Name = "Right Kick*",
                LeftyName = "Left Kick*",
                Type = DrumsHighwayItemIconType.Kick,
                ColorIndex = (int)FourLaneDrumsFret.Kick,
                Value = DrumsHighwayItem.Kick1x,
                MergesInto = DrumsHighwayItem.Kick2x,
                MergedResult = DrumsHighwayItem.Kick
            } },

            { DrumsHighwayItem.Kick2x, new() {
                Name = "Left Kick*",
                LeftyName = "Right Kick*",
                Type = DrumsHighwayItemIconType.Kick,
                ColorIndex = (int)FourLaneDrumsFret.DoubleKick,
                Value = DrumsHighwayItem.Kick2x,
                MergesInto = DrumsHighwayItem.Kick1x,
                MergedResult = DrumsHighwayItem.Kick
            } },

            { DrumsHighwayItem.Kick2xConditional, new() {
                Name = "Left Kick*",
                LeftyName = "Right Kick*",
                Type = DrumsHighwayItemIconType.Kick,
                ColorIndex = (int)FourLaneDrumsFret.DoubleKick,
                Value = DrumsHighwayItem.Kick2x,
                MergesInto = DrumsHighwayItem.Kick1x,
                MergedResult = DrumsHighwayItem.Kick
            } },

            { DrumsHighwayItem.FourLaneRed, new() {
                Name = "Red",
                LeftyName = "Green",
                Type = DrumsHighwayItemIconType.Drum,
                ColorIndex = (int)FourLaneDrumsFret.RedDrum,
                Value = DrumsHighwayItem.FourLaneRed
            } },

            { DrumsHighwayItem.FourLaneYellow, new() {
                Name = "Yellow",
                LeftyName = "Blue",
                Type = DrumsHighwayItemIconType.Combined,
                ColorIndex = (int)FourLaneDrumsFret.YellowDrum,
                Value = DrumsHighwayItem.FourLaneYellow,
                SplitsInto = (DrumsHighwayItem.FourLaneYellowCymbal, DrumsHighwayItem.FourLaneYellowDrum)
            } },

            { DrumsHighwayItem.FourLaneBlue, new() {
                Name = "Blue",
                LeftyName = "Yellow",
                Type = DrumsHighwayItemIconType.Combined,
                ColorIndex = (int)FourLaneDrumsFret.BlueDrum,
                Value = DrumsHighwayItem.FourLaneBlue,
                SplitsInto = (DrumsHighwayItem.FourLaneBlueCymbal, DrumsHighwayItem.FourLaneBlueDrum)
            } },

            { DrumsHighwayItem.FourLaneGreen, new() {
                Name = "Green",
                LeftyName = "Red",
                Type = DrumsHighwayItemIconType.Combined,
                ColorIndex = (int)FourLaneDrumsFret.GreenDrum,
                Value = DrumsHighwayItem.FourLaneGreen,
                SplitsInto = (DrumsHighwayItem.FourLaneGreenCymbal, DrumsHighwayItem.FourLaneGreenDrum)
            } },

            { DrumsHighwayItem.FourLaneYellowCymbal, new() {
                Name = "Yellow Cymbal",
                LeftyName = "Blue Cymbal",
                Type = DrumsHighwayItemIconType.Cymbal,
                ColorIndex = (int)FourLaneDrumsFret.YellowCymbal,
                Value = DrumsHighwayItem.FourLaneYellowCymbal,
                MergesInto = DrumsHighwayItem.FourLaneYellowDrum,
                MergedResult = DrumsHighwayItem.FourLaneYellow
            } },

            { DrumsHighwayItem.FourLaneBlueCymbal, new() {
                Name = "Blue Cymbal",
                LeftyName = "Yellow Cymbal",
                Type = DrumsHighwayItemIconType.Cymbal,
                ColorIndex = (int)FourLaneDrumsFret.BlueCymbal,
                Value = DrumsHighwayItem.FourLaneBlueCymbal,
                MergesInto = DrumsHighwayItem.FourLaneBlueDrum,
                MergedResult = DrumsHighwayItem.FourLaneBlue
            } },

            { DrumsHighwayItem.FourLaneGreenCymbal, new() {
                Name = "Green Cymbal",
                LeftyName = "Red Cymbal",
                Type = DrumsHighwayItemIconType.Cymbal,
                ColorIndex = (int)FourLaneDrumsFret.GreenCymbal,
                Value = DrumsHighwayItem.FourLaneGreenCymbal,
                MergesInto = DrumsHighwayItem.FourLaneGreenDrum,
                MergedResult = DrumsHighwayItem.FourLaneGreen
            } },

            { DrumsHighwayItem.FourLaneYellowDrum, new() {
                Name = "Yellow Drum",
                LeftyName = "Blue Drum",
                Type = DrumsHighwayItemIconType.Drum,
                ColorIndex = (int)FourLaneDrumsFret.YellowDrum,
                Value = DrumsHighwayItem.FourLaneYellowDrum,
                MergesInto = DrumsHighwayItem.FourLaneYellowCymbal,
                MergedResult = DrumsHighwayItem.FourLaneYellow
            } },

            { DrumsHighwayItem.FourLaneBlueDrum, new() {
                Name = "Blue Drum",
                LeftyName = "Yellow Drum",
                Type = DrumsHighwayItemIconType.Drum,
                ColorIndex = (int)FourLaneDrumsFret.BlueDrum,
                Value = DrumsHighwayItem.FourLaneBlueDrum,
                MergesInto = DrumsHighwayItem.FourLaneBlueCymbal,
                MergedResult = DrumsHighwayItem.FourLaneBlue
            } },

            { DrumsHighwayItem.FourLaneGreenDrum, new() {
                Name = "Green Drum",
                LeftyName = "Red Drum",
                Type = DrumsHighwayItemIconType.Drum,
                ColorIndex = (int)FourLaneDrumsFret.GreenDrum,
                Value = DrumsHighwayItem.FourLaneGreenDrum,
                MergesInto = DrumsHighwayItem.FourLaneGreenCymbal,
                MergedResult = DrumsHighwayItem.FourLaneGreen
            } },
        };

        public static Dictionary<DrumsHighwayItem, HighwayOrderingItemSpec> FIVE_LANE_SPECS { get; } = new()
        {
            { DrumsHighwayItem.Kick, new() {
                Name = "Kick",
                LeftyName = "Kick",
                Type = DrumsHighwayItemIconType.Kick,
                ColorIndex = (int)FiveLaneDrumsFret.Kick,
                Value = DrumsHighwayItem.Kick,
                SplitsInto = (DrumsHighwayItem.Kick2x, DrumsHighwayItem.Kick1x)
            } },

            { DrumsHighwayItem.Kick1x, new() {
                Name = "Right Kick*",
                LeftyName = "Left Kick*",
                Type = DrumsHighwayItemIconType.Kick,
                ColorIndex = (int)FiveLaneDrumsFret.Kick,
                Value = DrumsHighwayItem.Kick1x,
                MergesInto = DrumsHighwayItem.Kick2x,
                MergedResult = DrumsHighwayItem.Kick
            } },

            { DrumsHighwayItem.Kick2x, new() {
                Name = "Left Kick*",
                LeftyName = "Right Kick*",
                Type = DrumsHighwayItemIconType.Kick,
                ColorIndex = (int)FiveLaneDrumsFret.DoubleKick,
                Value = DrumsHighwayItem.Kick2x,
                MergesInto = DrumsHighwayItem.Kick1x,
                MergedResult = DrumsHighwayItem.Kick
            } },

            { DrumsHighwayItem.Kick2xConditional, new() {
                Name = "Left Kick*",
                LeftyName = "Right Kick*",
                Type = DrumsHighwayItemIconType.Kick,
                ColorIndex = (int)FiveLaneDrumsFret.DoubleKick,
                Value = DrumsHighwayItem.Kick2x,
                MergesInto = DrumsHighwayItem.Kick1x,
                MergedResult = DrumsHighwayItem.Kick
            } },

            { DrumsHighwayItem.FiveLaneRed, new() {
                Name = "Red",
                LeftyName = "Green",
                Type = DrumsHighwayItemIconType.Drum,
                ColorIndex = (int)FiveLaneDrumsFret.Red,
                Value = DrumsHighwayItem.FiveLaneRed
            } },

            { DrumsHighwayItem.FiveLaneYellow, new() {
                Name = "Yellow",
                LeftyName = "Orange",
                Type = DrumsHighwayItemIconType.Cymbal,
                ColorIndex = (int)FiveLaneDrumsFret.Yellow,
                Value = DrumsHighwayItem.FiveLaneYellow
            } },

            { DrumsHighwayItem.FiveLaneBlue, new() {
                Name = "Blue",
                LeftyName = "Blue",
                Type = DrumsHighwayItemIconType.Drum,
                ColorIndex = (int)FiveLaneDrumsFret.Blue,
                Value = DrumsHighwayItem.FiveLaneBlue
            } },

            { DrumsHighwayItem.FiveLaneOrange, new() {
                Name = "Orange",
                LeftyName = "Yellow",
                Type = DrumsHighwayItemIconType.Cymbal,
                ColorIndex = (int)FiveLaneDrumsFret.Orange,
                Value = DrumsHighwayItem.FiveLaneOrange
            } },

            { DrumsHighwayItem.FiveLaneGreen, new() {
                Name = "Green",
                LeftyName = "Red",
                Type = DrumsHighwayItemIconType.Drum,
                ColorIndex = (int)FiveLaneDrumsFret.Green,
                Value = DrumsHighwayItem.FiveLaneGreen
            } },
        };
    }
}
