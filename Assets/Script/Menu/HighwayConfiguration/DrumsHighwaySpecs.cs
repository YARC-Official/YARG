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
                Name = KICK,
                LeftyName = KICK,
                Type = DrumsHighwayItemIconType.Kick,
                ColorIndex = (int)FourLaneDrumsFret.Kick,
                Value = DrumsHighwayItem.Kick,
                SplitsInto = (DrumsHighwayItem.Kick2x, DrumsHighwayItem.Kick1x)
            } },

            { DrumsHighwayItem.Kick1x, new() {
                Name = RIGHT_KICK,
                LeftyName = LEFT_KICK,
                Type = DrumsHighwayItemIconType.Kick,
                ColorIndex = (int)FourLaneDrumsFret.Kick,
                Value = DrumsHighwayItem.Kick1x,
                MergesInto = DrumsHighwayItem.Kick2x,
                MergedResult = DrumsHighwayItem.Kick
            } },

            { DrumsHighwayItem.Kick2x, new() {
                Name = LEFT_KICK,
                LeftyName = RIGHT_KICK,
                Type = DrumsHighwayItemIconType.Kick,
                ColorIndex = (int)FourLaneDrumsFret.DoubleKick,
                Value = DrumsHighwayItem.Kick2x,
                MergesInto = DrumsHighwayItem.Kick1x,
                MergedResult = DrumsHighwayItem.Kick
            } },

            { DrumsHighwayItem.Kick2xConditional, new() {
                Name = LEFT_KICK,
                LeftyName = RIGHT_KICK,
                Type = DrumsHighwayItemIconType.Kick,
                ColorIndex = (int)FourLaneDrumsFret.DoubleKick,
                Value = DrumsHighwayItem.Kick2x,
                MergesInto = DrumsHighwayItem.Kick1x,
                MergedResult = DrumsHighwayItem.Kick
            } },

            { DrumsHighwayItem.FourLaneRed, new() {
                Name = RED,
                LeftyName = GREEN,
                Type = DrumsHighwayItemIconType.Drum,
                ColorIndex = (int)FourLaneDrumsFret.RedDrum,
                Value = DrumsHighwayItem.FourLaneRed
            } },

            { DrumsHighwayItem.FourLaneYellow, new() {
                Name = YELLOW,
                LeftyName = BLUE,
                Type = DrumsHighwayItemIconType.Combined,
                ColorIndex = (int)FourLaneDrumsFret.YellowDrum,
                Value = DrumsHighwayItem.FourLaneYellow
            } },

            { DrumsHighwayItem.FourLaneBlue, new() {
                Name = BLUE,
                LeftyName = YELLOW,
                Type = DrumsHighwayItemIconType.Combined,
                ColorIndex = (int)FourLaneDrumsFret.BlueDrum,
                Value = DrumsHighwayItem.FourLaneBlue
            } },

            { DrumsHighwayItem.FourLaneGreen, new() {
                Name = GREEN,
                LeftyName = RED,
                Type = DrumsHighwayItemIconType.Combined,
                ColorIndex = (int)FourLaneDrumsFret.GreenDrum,
                Value = DrumsHighwayItem.FourLaneGreen
            } },
        };

        public static Dictionary<DrumsHighwayItem, HighwayOrderingItemSpec> PRO_DRUMS_SPECS { get; } = new()
        {
           { DrumsHighwayItem.Kick, new() {
                Name = KICK,
                LeftyName = KICK,
                Type = DrumsHighwayItemIconType.Kick,
                ColorIndex = (int)FourLaneDrumsFret.Kick,
                Value = DrumsHighwayItem.Kick,
                SplitsInto = (DrumsHighwayItem.Kick2x, DrumsHighwayItem.Kick1x)
            } },

            { DrumsHighwayItem.Kick1x, new() {
                Name = RIGHT_KICK,
                LeftyName = LEFT_KICK,
                Type = DrumsHighwayItemIconType.Kick,
                ColorIndex = (int)FourLaneDrumsFret.Kick,
                Value = DrumsHighwayItem.Kick1x,
                MergesInto = DrumsHighwayItem.Kick2x,
                MergedResult = DrumsHighwayItem.Kick
            } },

            { DrumsHighwayItem.Kick2x, new() {
                Name = LEFT_KICK,
                LeftyName = RIGHT_KICK,
                Type = DrumsHighwayItemIconType.Kick,
                ColorIndex = (int)FourLaneDrumsFret.DoubleKick,
                Value = DrumsHighwayItem.Kick2x,
                MergesInto = DrumsHighwayItem.Kick1x,
                MergedResult = DrumsHighwayItem.Kick
            } },

            { DrumsHighwayItem.Kick2xConditional, new() {
                Name = LEFT_KICK,
                LeftyName = RIGHT_KICK,
                Type = DrumsHighwayItemIconType.Kick,
                ColorIndex = (int)FourLaneDrumsFret.DoubleKick,
                Value = DrumsHighwayItem.Kick2x,
                MergesInto = DrumsHighwayItem.Kick1x,
                MergedResult = DrumsHighwayItem.Kick
            } },

            { DrumsHighwayItem.FourLaneRed, new() {
                Name = RED,
                LeftyName = GREEN,
                Type = DrumsHighwayItemIconType.Drum,
                ColorIndex = (int)FourLaneDrumsFret.RedDrum,
                Value = DrumsHighwayItem.FourLaneRed
            } },

            { DrumsHighwayItem.FourLaneYellow, new() {
                Name = YELLOW,
                LeftyName = BLUE,
                Type = DrumsHighwayItemIconType.Combined,
                ColorIndex = (int)FourLaneDrumsFret.YellowDrum,
                Value = DrumsHighwayItem.FourLaneYellow,
                SplitsInto = (DrumsHighwayItem.FourLaneYellowCymbal, DrumsHighwayItem.FourLaneYellowDrum)
            } },

            { DrumsHighwayItem.FourLaneBlue, new() {
                Name = BLUE,
                LeftyName = YELLOW,
                Type = DrumsHighwayItemIconType.Combined,
                ColorIndex = (int)FourLaneDrumsFret.BlueDrum,
                Value = DrumsHighwayItem.FourLaneBlue,
                SplitsInto = (DrumsHighwayItem.FourLaneBlueCymbal, DrumsHighwayItem.FourLaneBlueDrum)
            } },

            { DrumsHighwayItem.FourLaneGreen, new() {
                Name = GREEN,
                LeftyName = RED,
                Type = DrumsHighwayItemIconType.Combined,
                ColorIndex = (int)FourLaneDrumsFret.GreenDrum,
                Value = DrumsHighwayItem.FourLaneGreen,
                SplitsInto = (DrumsHighwayItem.FourLaneGreenCymbal, DrumsHighwayItem.FourLaneGreenDrum)
            } },

            { DrumsHighwayItem.FourLaneYellowCymbal, new() {
                Name = YELLOW_CYMBAL,
                LeftyName = BLUE_CYMBAL,
                Type = DrumsHighwayItemIconType.Cymbal,
                ColorIndex = (int)FourLaneDrumsFret.YellowCymbal,
                Value = DrumsHighwayItem.FourLaneYellowCymbal,
                MergesInto = DrumsHighwayItem.FourLaneYellowDrum,
                MergedResult = DrumsHighwayItem.FourLaneYellow
            } },

            { DrumsHighwayItem.FourLaneBlueCymbal, new() {
                Name = BLUE_CYMBAL,
                LeftyName = YELLOW_CYMBAL,
                Type = DrumsHighwayItemIconType.Cymbal,
                ColorIndex = (int)FourLaneDrumsFret.BlueCymbal,
                Value = DrumsHighwayItem.FourLaneBlueCymbal,
                MergesInto = DrumsHighwayItem.FourLaneBlueDrum,
                MergedResult = DrumsHighwayItem.FourLaneBlue
            } },

            { DrumsHighwayItem.FourLaneGreenCymbal, new() {
                Name = GREEN_CYMBAL,
                LeftyName = RED_CYMBAL,
                Type = DrumsHighwayItemIconType.Cymbal,
                ColorIndex = (int)FourLaneDrumsFret.GreenCymbal,
                Value = DrumsHighwayItem.FourLaneGreenCymbal,
                MergesInto = DrumsHighwayItem.FourLaneGreenDrum,
                MergedResult = DrumsHighwayItem.FourLaneGreen
            } },

            { DrumsHighwayItem.FourLaneYellowDrum, new() {
                Name = YELLOW_DRUM,
                LeftyName = BLUE_DRUM,
                Type = DrumsHighwayItemIconType.Drum,
                ColorIndex = (int)FourLaneDrumsFret.YellowDrum,
                Value = DrumsHighwayItem.FourLaneYellowDrum,
                MergesInto = DrumsHighwayItem.FourLaneYellowCymbal,
                MergedResult = DrumsHighwayItem.FourLaneYellow
            } },

            { DrumsHighwayItem.FourLaneBlueDrum, new() {
                Name = BLUE_DRUM,
                LeftyName = YELLOW_DRUM,
                Type = DrumsHighwayItemIconType.Drum,
                ColorIndex = (int)FourLaneDrumsFret.BlueDrum,
                Value = DrumsHighwayItem.FourLaneBlueDrum,
                MergesInto = DrumsHighwayItem.FourLaneBlueCymbal,
                MergedResult = DrumsHighwayItem.FourLaneBlue
            } },

            { DrumsHighwayItem.FourLaneGreenDrum, new() {
                Name = GREEN_DRUM,
                LeftyName = RED_DRUM,
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
                Name = KICK,
                LeftyName = KICK,
                Type = DrumsHighwayItemIconType.Kick,
                ColorIndex = (int)FiveLaneDrumsFret.Kick,
                Value = DrumsHighwayItem.Kick,
                SplitsInto = (DrumsHighwayItem.Kick2x, DrumsHighwayItem.Kick1x)
            } },

            { DrumsHighwayItem.Kick1x, new() {
                Name = RIGHT_KICK,
                LeftyName = LEFT_KICK,
                Type = DrumsHighwayItemIconType.Kick,
                ColorIndex = (int)FiveLaneDrumsFret.Kick,
                Value = DrumsHighwayItem.Kick1x,
                MergesInto = DrumsHighwayItem.Kick2x,
                MergedResult = DrumsHighwayItem.Kick
            } },

            { DrumsHighwayItem.Kick2x, new() {
                Name = LEFT_KICK,
                LeftyName = RIGHT_KICK,
                Type = DrumsHighwayItemIconType.Kick,
                ColorIndex = (int)FiveLaneDrumsFret.DoubleKick,
                Value = DrumsHighwayItem.Kick2x,
                MergesInto = DrumsHighwayItem.Kick1x,
                MergedResult = DrumsHighwayItem.Kick
            } },

            { DrumsHighwayItem.Kick2xConditional, new() {
                Name = LEFT_KICK,
                LeftyName = RIGHT_KICK,
                Type = DrumsHighwayItemIconType.Kick,
                ColorIndex = (int)FiveLaneDrumsFret.DoubleKick,
                Value = DrumsHighwayItem.Kick2x,
                MergesInto = DrumsHighwayItem.Kick1x,
                MergedResult = DrumsHighwayItem.Kick
            } },

            { DrumsHighwayItem.FiveLaneRed, new() {
                Name = RED,
                LeftyName = GREEN,
                Type = DrumsHighwayItemIconType.Drum,
                ColorIndex = (int)FiveLaneDrumsFret.Red,
                Value = DrumsHighwayItem.FiveLaneRed
            } },

            { DrumsHighwayItem.FiveLaneYellow, new() {
                Name = YELLOW,
                LeftyName = ORANGE,
                Type = DrumsHighwayItemIconType.Cymbal,
                ColorIndex = (int)FiveLaneDrumsFret.Yellow,
                Value = DrumsHighwayItem.FiveLaneYellow
            } },

            { DrumsHighwayItem.FiveLaneBlue, new() {
                Name = BLUE,
                LeftyName = BLUE,
                Type = DrumsHighwayItemIconType.Drum,
                ColorIndex = (int)FiveLaneDrumsFret.Blue,
                Value = DrumsHighwayItem.FiveLaneBlue
            } },

            { DrumsHighwayItem.FiveLaneOrange, new() {
                Name = ORANGE,
                LeftyName = YELLOW,
                Type = DrumsHighwayItemIconType.Cymbal,
                ColorIndex = (int)FiveLaneDrumsFret.Orange,
                Value = DrumsHighwayItem.FiveLaneOrange
            } },

            { DrumsHighwayItem.FiveLaneGreen, new() {
                Name = GREEN,
                LeftyName = RED,
                Type = DrumsHighwayItemIconType.Drum,
                ColorIndex = (int)FiveLaneDrumsFret.Green,
                Value = DrumsHighwayItem.FiveLaneGreen
            } },
        };

        // Localization codes
        private const string RED = "Red";
        private const string RED_CYMBAL = "RedCymbal";
        private const string RED_DRUM = "RedDrum";
        private const string YELLOW = "Yellow";
        private const string YELLOW_CYMBAL = "YellowCymbal";
        private const string YELLOW_DRUM = "YellowDrum";
        private const string BLUE = "Blue";
        private const string BLUE_CYMBAL = "BlueCymbal";
        private const string BLUE_DRUM = "BlueDrum";
        private const string GREEN = "Green";
        private const string GREEN_CYMBAL = "GreenCymbal";
        private const string GREEN_DRUM = "GreenDrum";
        private const string ORANGE = "Orange";
        private const string KICK = "Kick";
        private const string RIGHT_KICK = "RightKick";
        private const string LEFT_KICK = "LeftKick";
    }
}
