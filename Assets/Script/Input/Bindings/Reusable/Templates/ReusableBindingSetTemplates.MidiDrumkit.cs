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
            { ActionStrings.DRUMS_KICK,                new(ActionStrings.DRUMS_KICK,                  BindingType.Impulse, (int)EliteDrumsAction.Kick)},

            { ActionStrings.ELITE_DRUMS_STOMP,         new(ActionStrings.ELITE_DRUMS_STOMP,           BindingType.Impulse, (int)EliteDrumsAction.EliteStomp) },
            { ActionStrings.ELITE_DRUMS_SPLASH,        new(ActionStrings.ELITE_DRUMS_SPLASH,          BindingType.Impulse, (int)EliteDrumsAction.EliteSplash) },
            { ActionStrings.ELITE_DRUMS_SNARE,         new(ActionStrings.ELITE_DRUMS_SNARE,           BindingType.Impulse, (int)EliteDrumsAction.EliteSnare) },
            { ActionStrings.ELITE_DRUMS_CLOSED_HI_HAT, new(ActionStrings.ELITE_DRUMS_CLOSED_HI_HAT,   BindingType.Impulse, (int)EliteDrumsAction.EliteClosedHiHat) },
            { ActionStrings.ELITE_DRUMS_SIZZLE_HI_HAT, new(ActionStrings.ELITE_DRUMS_SIZZLE_HI_HAT,   BindingType.Impulse, (int)EliteDrumsAction.EliteSizzleHiHat) },
            { ActionStrings.ELITE_DRUMS_OPEN_HI_HAT,   new(ActionStrings.ELITE_DRUMS_OPEN_HI_HAT,     BindingType.Impulse, (int)EliteDrumsAction.EliteOpenHiHat) },
            { ActionStrings.ELITE_DRUMS_LEFT_CRASH,    new(ActionStrings.ELITE_DRUMS_LEFT_CRASH,      BindingType.Impulse, (int)EliteDrumsAction.EliteLeftCrash) },
            { ActionStrings.ELITE_DRUMS_TOM_1,         new(ActionStrings.ELITE_DRUMS_TOM_1,           BindingType.Impulse, (int)EliteDrumsAction.EliteTom1) },
            { ActionStrings.ELITE_DRUMS_TOM_2,         new(ActionStrings.ELITE_DRUMS_TOM_2,           BindingType.Impulse, (int)EliteDrumsAction.EliteTom2) },
            { ActionStrings.ELITE_DRUMS_TOM_3,         new(ActionStrings.ELITE_DRUMS_TOM_3,           BindingType.Impulse, (int)EliteDrumsAction.EliteTom3) },
            { ActionStrings.ELITE_DRUMS_RIDE,          new(ActionStrings.ELITE_DRUMS_RIDE,            BindingType.Impulse, (int)EliteDrumsAction.EliteRide) },
            { ActionStrings.ELITE_DRUMS_RIGHT_CRASH,   new(ActionStrings.ELITE_DRUMS_RIGHT_CRASH,     BindingType.Impulse, (int)EliteDrumsAction.EliteRightCrash) },

            { ActionStrings.ELITE_DRUMS_4L_RED,        new(ActionStrings.ELITE_DRUMS_4L_RED,          BindingType.Impulse, (int)EliteDrumsAction.FourLaneRedDrum) },
            { ActionStrings.ELITE_DRUMS_4L_YTOM,       new(ActionStrings.ELITE_DRUMS_4L_YTOM,         BindingType.Impulse, (int)EliteDrumsAction.FourLaneYellowDrum) },
            { ActionStrings.ELITE_DRUMS_4L_BTOM,       new(ActionStrings.ELITE_DRUMS_4L_BTOM,         BindingType.Impulse, (int)EliteDrumsAction.FourLaneBlueDrum) },
            { ActionStrings.ELITE_DRUMS_4L_GTOM,       new(ActionStrings.ELITE_DRUMS_4L_GTOM,         BindingType.Impulse, (int)EliteDrumsAction.FourLaneGreenDrum) },
            { ActionStrings.ELITE_DRUMS_4L_YCYM,       new(ActionStrings.ELITE_DRUMS_4L_YCYM,         BindingType.Impulse, (int)EliteDrumsAction.FourLaneYellowCymbal) },
            { ActionStrings.ELITE_DRUMS_4L_BCYM,       new(ActionStrings.ELITE_DRUMS_4L_BCYM,         BindingType.Impulse, (int)EliteDrumsAction.FourLaneBlueCymbal) },
            { ActionStrings.ELITE_DRUMS_4L_GCYM,       new(ActionStrings.ELITE_DRUMS_4L_GCYM,         BindingType.Impulse, (int)EliteDrumsAction.FourLaneGreenCymbal) },

            { ActionStrings.ELITE_DRUMS_5L_RED,        new(ActionStrings.ELITE_DRUMS_5L_RED,          BindingType.Impulse, (int)EliteDrumsAction.FiveLaneRedDrum) },
            { ActionStrings.ELITE_DRUMS_5L_BLUE,       new(ActionStrings.ELITE_DRUMS_5L_BLUE,         BindingType.Impulse, (int)EliteDrumsAction.FiveLaneBlueDrum) },
            { ActionStrings.ELITE_DRUMS_5L_GREEN,      new(ActionStrings.ELITE_DRUMS_5L_GREEN,        BindingType.Impulse, (int)EliteDrumsAction.FiveLaneGreenDrum) },
            { ActionStrings.ELITE_DRUMS_5L_YELLOW,     new(ActionStrings.ELITE_DRUMS_5L_YELLOW,       BindingType.Impulse, (int)EliteDrumsAction.FiveLaneYellowCymbal) },
            { ActionStrings.ELITE_DRUMS_5L_ORANGE,     new(ActionStrings.ELITE_DRUMS_5L_ORANGE,       BindingType.Impulse, (int)EliteDrumsAction.FiveLaneOrangeCymbal) },
        };
    }
}
