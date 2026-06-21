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

            { DrumsHighwayItem.Red, new() {
                Name = RED,
                LeftyName = GREEN,
                Type = DrumsHighwayItemIconType.Drum,
                ColorIndex = (int)FourLaneDrumsFret.RedDrum,
                Value = DrumsHighwayItem.Red
            } },

            { DrumsHighwayItem.Yellow, new() {
                Name = YELLOW,
                LeftyName = BLUE,
                Type = DrumsHighwayItemIconType.Combined,
                ColorIndex = (int)FourLaneDrumsFret.YellowDrum,
                Value = DrumsHighwayItem.Yellow
            } },

            { DrumsHighwayItem.Blue, new() {
                Name = BLUE,
                LeftyName = YELLOW,
                Type = DrumsHighwayItemIconType.Combined,
                ColorIndex = (int)FourLaneDrumsFret.BlueDrum,
                Value = DrumsHighwayItem.Blue
            } },

            { DrumsHighwayItem.Green, new() {
                Name = GREEN,
                LeftyName = RED,
                Type = DrumsHighwayItemIconType.Combined,
                ColorIndex = (int)FourLaneDrumsFret.GreenDrum,
                Value = DrumsHighwayItem.Green
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

            { DrumsHighwayItem.Red, new() {
                Name = RED,
                LeftyName = GREEN,
                Type = DrumsHighwayItemIconType.Drum,
                ColorIndex = (int)FourLaneDrumsFret.RedDrum,
                Value = DrumsHighwayItem.Red
            } },

            { DrumsHighwayItem.Yellow, new() {
                Name = YELLOW,
                LeftyName = BLUE,
                Type = DrumsHighwayItemIconType.Combined,
                ColorIndex = (int)FourLaneDrumsFret.YellowDrum,
                Value = DrumsHighwayItem.Yellow,
                SplitsInto = (DrumsHighwayItem.YellowCymbal, DrumsHighwayItem.YellowDrum)
            } },

            { DrumsHighwayItem.Blue, new() {
                Name = BLUE,
                LeftyName = YELLOW,
                Type = DrumsHighwayItemIconType.Combined,
                ColorIndex = (int)FourLaneDrumsFret.BlueDrum,
                Value = DrumsHighwayItem.Blue,
                SplitsInto = (DrumsHighwayItem.BlueCymbal, DrumsHighwayItem.BlueDrum)
            } },

            { DrumsHighwayItem.Green, new() {
                Name = GREEN,
                LeftyName = RED,
                Type = DrumsHighwayItemIconType.Combined,
                ColorIndex = (int)FourLaneDrumsFret.GreenDrum,
                Value = DrumsHighwayItem.Green,
                SplitsInto = (DrumsHighwayItem.GreenCymbal, DrumsHighwayItem.GreenDrum)
            } },

            { DrumsHighwayItem.YellowCymbal, new() {
                Name = YELLOW_CYMBAL,
                LeftyName = BLUE_CYMBAL,
                Type = DrumsHighwayItemIconType.Cymbal,
                ColorIndex = (int)FourLaneDrumsFret.YellowCymbal,
                Value = DrumsHighwayItem.YellowCymbal,
                MergesInto = DrumsHighwayItem.YellowDrum,
                MergedResult = DrumsHighwayItem.Yellow
            } },

            { DrumsHighwayItem.BlueCymbal, new() {
                Name = BLUE_CYMBAL,
                LeftyName = YELLOW_CYMBAL,
                Type = DrumsHighwayItemIconType.Cymbal,
                ColorIndex = (int)FourLaneDrumsFret.BlueCymbal,
                Value = DrumsHighwayItem.BlueCymbal,
                MergesInto = DrumsHighwayItem.BlueDrum,
                MergedResult = DrumsHighwayItem.Blue
            } },

            { DrumsHighwayItem.GreenCymbal, new() {
                Name = GREEN_CYMBAL,
                LeftyName = RED_CYMBAL,
                Type = DrumsHighwayItemIconType.Cymbal,
                ColorIndex = (int)FourLaneDrumsFret.GreenCymbal,
                Value = DrumsHighwayItem.GreenCymbal,
                MergesInto = DrumsHighwayItem.GreenDrum,
                MergedResult = DrumsHighwayItem.Green
            } },

            { DrumsHighwayItem.YellowDrum, new() {
                Name = YELLOW_DRUM,
                LeftyName = BLUE_DRUM,
                Type = DrumsHighwayItemIconType.Drum,
                ColorIndex = (int)FourLaneDrumsFret.YellowDrum,
                Value = DrumsHighwayItem.YellowDrum,
                MergesInto = DrumsHighwayItem.YellowCymbal,
                MergedResult = DrumsHighwayItem.Yellow
            } },

            { DrumsHighwayItem.BlueDrum, new() {
                Name = BLUE_DRUM,
                LeftyName = YELLOW_DRUM,
                Type = DrumsHighwayItemIconType.Drum,
                ColorIndex = (int)FourLaneDrumsFret.BlueDrum,
                Value = DrumsHighwayItem.BlueDrum,
                MergesInto = DrumsHighwayItem.BlueCymbal,
                MergedResult = DrumsHighwayItem.Blue
            } },

            { DrumsHighwayItem.GreenDrum, new() {
                Name = GREEN_DRUM,
                LeftyName = RED_DRUM,
                Type = DrumsHighwayItemIconType.Drum,
                ColorIndex = (int)FourLaneDrumsFret.GreenDrum,
                Value = DrumsHighwayItem.GreenDrum,
                MergesInto = DrumsHighwayItem.GreenCymbal,
                MergedResult = DrumsHighwayItem.Green
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

            { DrumsHighwayItem.Red, new() {
                Name = RED,
                LeftyName = GREEN,
                Type = DrumsHighwayItemIconType.Drum,
                ColorIndex = (int)FiveLaneDrumsFret.Red,
                Value = DrumsHighwayItem.Red
            } },

            { DrumsHighwayItem.Yellow, new() {
                Name = YELLOW,
                LeftyName = ORANGE,
                Type = DrumsHighwayItemIconType.Cymbal,
                ColorIndex = (int)FiveLaneDrumsFret.Yellow,
                Value = DrumsHighwayItem.Yellow
            } },

            { DrumsHighwayItem.Blue, new() {
                Name = BLUE,
                LeftyName = BLUE,
                Type = DrumsHighwayItemIconType.Drum,
                ColorIndex = (int)FiveLaneDrumsFret.Blue,
                Value = DrumsHighwayItem.Blue
            } },

            { DrumsHighwayItem.Orange, new() {
                Name = ORANGE,
                LeftyName = YELLOW,
                Type = DrumsHighwayItemIconType.Cymbal,
                ColorIndex = (int)FiveLaneDrumsFret.Orange,
                Value = DrumsHighwayItem.Orange
            } },

            { DrumsHighwayItem.Green, new() {
                Name = GREEN,
                LeftyName = RED,
                Type = DrumsHighwayItemIconType.Drum,
                ColorIndex = (int)FiveLaneDrumsFret.Green,
                Value = DrumsHighwayItem.Green
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
