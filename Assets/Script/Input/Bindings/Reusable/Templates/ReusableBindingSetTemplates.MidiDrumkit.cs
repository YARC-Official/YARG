using System.Collections.Generic;
using YARG.Core.Input;
using YARG.Helpers;

namespace YARG.Input.Bindings
{
    public static partial class ReusableBindingSetTemplates
    {
        public static Dictionary<string, InputActionInfo> ELITE_DRUMS = new()
        {
            // Kicks are bound universally across drum formats
            { ActionStrings.DRUMS_KICK,                new(ActionStrings.DRUMS_KICK,                  BindingType.DrumButton, (int)EliteDrumsAction.Kick)},

            { ActionStrings.ELITE_DRUMS_STOMP,         new(ActionStrings.ELITE_DRUMS_STOMP,           BindingType.DrumButton, (int)EliteDrumsAction.EliteStomp) },
            { ActionStrings.ELITE_DRUMS_SPLASH,        new(ActionStrings.ELITE_DRUMS_SPLASH,          BindingType.DrumButton, (int)EliteDrumsAction.EliteSplash) },
            { ActionStrings.ELITE_DRUMS_SNARE,         new(ActionStrings.ELITE_DRUMS_SNARE,           BindingType.DrumButton, (int)EliteDrumsAction.EliteSnare) },
            { ActionStrings.ELITE_DRUMS_CLOSED_HI_HAT, new(ActionStrings.ELITE_DRUMS_CLOSED_HI_HAT,   BindingType.DrumButton, (int)EliteDrumsAction.EliteClosedHiHat) },
            { ActionStrings.ELITE_DRUMS_SIZZLE_HI_HAT, new(ActionStrings.ELITE_DRUMS_SIZZLE_HI_HAT,   BindingType.DrumButton, (int)EliteDrumsAction.EliteSizzleHiHat) },
            { ActionStrings.ELITE_DRUMS_OPEN_HI_HAT,   new(ActionStrings.ELITE_DRUMS_OPEN_HI_HAT,     BindingType.DrumButton, (int)EliteDrumsAction.EliteOpenHiHat) },
            { ActionStrings.ELITE_DRUMS_LEFT_CRASH,    new(ActionStrings.ELITE_DRUMS_LEFT_CRASH,      BindingType.DrumButton, (int)EliteDrumsAction.EliteLeftCrash) },
            { ActionStrings.ELITE_DRUMS_TOM_1,         new(ActionStrings.ELITE_DRUMS_TOM_1,           BindingType.DrumButton, (int)EliteDrumsAction.EliteTom1) },
            { ActionStrings.ELITE_DRUMS_TOM_2,         new(ActionStrings.ELITE_DRUMS_TOM_2,           BindingType.DrumButton, (int)EliteDrumsAction.EliteTom2) },
            { ActionStrings.ELITE_DRUMS_TOM_3,         new(ActionStrings.ELITE_DRUMS_TOM_3,           BindingType.DrumButton, (int)EliteDrumsAction.EliteTom3) },
            { ActionStrings.ELITE_DRUMS_RIDE,          new(ActionStrings.ELITE_DRUMS_RIDE,            BindingType.DrumButton, (int)EliteDrumsAction.EliteRide) },
            { ActionStrings.ELITE_DRUMS_RIGHT_CRASH,   new(ActionStrings.ELITE_DRUMS_RIGHT_CRASH,     BindingType.DrumButton, (int)EliteDrumsAction.EliteRightCrash) },

            { ActionStrings.ELITE_DRUMS_4L_RED,        new(ActionStrings.ELITE_DRUMS_4L_RED,          BindingType.DrumButton, (int)EliteDrumsAction.FourLaneRedDrum) },
            { ActionStrings.ELITE_DRUMS_4L_YTOM,       new(ActionStrings.ELITE_DRUMS_4L_YTOM,         BindingType.DrumButton, (int)EliteDrumsAction.FourLaneYellowDrum) },
            { ActionStrings.ELITE_DRUMS_4L_BTOM,       new(ActionStrings.ELITE_DRUMS_4L_BTOM,         BindingType.DrumButton, (int)EliteDrumsAction.FourLaneBlueDrum) },
            { ActionStrings.ELITE_DRUMS_4L_GTOM,       new(ActionStrings.ELITE_DRUMS_4L_GTOM,         BindingType.DrumButton, (int)EliteDrumsAction.FourLaneGreenDrum) },
            { ActionStrings.ELITE_DRUMS_4L_YCYM,       new(ActionStrings.ELITE_DRUMS_4L_YCYM,         BindingType.DrumButton, (int)EliteDrumsAction.FourLaneYellowCymbal) },
            { ActionStrings.ELITE_DRUMS_4L_BCYM,       new(ActionStrings.ELITE_DRUMS_4L_BCYM,         BindingType.DrumButton, (int)EliteDrumsAction.FourLaneBlueCymbal) },
            { ActionStrings.ELITE_DRUMS_4L_GCYM,       new(ActionStrings.ELITE_DRUMS_4L_GCYM,         BindingType.DrumButton, (int)EliteDrumsAction.FourLaneGreenCymbal) },

            { ActionStrings.ELITE_DRUMS_5L_RED,        new(ActionStrings.ELITE_DRUMS_5L_RED,          BindingType.DrumButton, (int)EliteDrumsAction.FiveLaneRedDrum) },
            { ActionStrings.ELITE_DRUMS_5L_BLUE,       new(ActionStrings.ELITE_DRUMS_5L_BLUE,         BindingType.DrumButton, (int)EliteDrumsAction.FiveLaneBlueDrum) },
            { ActionStrings.ELITE_DRUMS_5L_GREEN,      new(ActionStrings.ELITE_DRUMS_5L_GREEN,        BindingType.DrumButton, (int)EliteDrumsAction.FiveLaneGreenDrum) },
            { ActionStrings.ELITE_DRUMS_5L_YELLOW,     new(ActionStrings.ELITE_DRUMS_5L_YELLOW,       BindingType.DrumButton, (int)EliteDrumsAction.FiveLaneYellowCymbal) },
            { ActionStrings.ELITE_DRUMS_5L_ORANGE,     new(ActionStrings.ELITE_DRUMS_5L_ORANGE,       BindingType.DrumButton, (int)EliteDrumsAction.FiveLaneOrangeCymbal) },
        };
    }
}
