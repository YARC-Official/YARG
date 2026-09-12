using System.Collections.Generic;
using YARG.Core.Input;

namespace YARG.Input.Bindings
{
    public static partial class ReusableBindingSetTemplates
    {
        public static Dictionary<string, InputActionInfo> ELITE_DRUMS = new()
        {
            // Kicks are bound universally across drum formats
            { ControlStrings.DRUMS_KICK,                new(ControlStrings.DRUMS_KICK,                  BindingType.DrumButton, (int)EliteDrumsAction.Kick)},

            { ControlStrings.ELITE_DRUMS_STOMP,         new(ControlStrings.ELITE_DRUMS_STOMP,           BindingType.DrumButton, (int)EliteDrumsAction.EliteStomp) },
            { ControlStrings.ELITE_DRUMS_SPLASH,        new(ControlStrings.ELITE_DRUMS_SPLASH,          BindingType.DrumButton, (int)EliteDrumsAction.EliteSplash) },
            { ControlStrings.ELITE_DRUMS_SNARE,         new(ControlStrings.ELITE_DRUMS_SNARE,           BindingType.DrumButton, (int)EliteDrumsAction.EliteSnare) },
            { ControlStrings.ELITE_DRUMS_CLOSED_HI_HAT, new(ControlStrings.ELITE_DRUMS_CLOSED_HI_HAT,   BindingType.DrumButton, (int)EliteDrumsAction.EliteClosedHiHat) },
            { ControlStrings.ELITE_DRUMS_SIZZLE_HI_HAT, new(ControlStrings.ELITE_DRUMS_SIZZLE_HI_HAT,   BindingType.DrumButton, (int)EliteDrumsAction.EliteSizzleHiHat) },
            { ControlStrings.ELITE_DRUMS_OPEN_HI_HAT,   new(ControlStrings.ELITE_DRUMS_OPEN_HI_HAT,     BindingType.DrumButton, (int)EliteDrumsAction.EliteOpenHiHat) },
            { ControlStrings.ELITE_DRUMS_LEFT_CRASH,    new(ControlStrings.ELITE_DRUMS_LEFT_CRASH,      BindingType.DrumButton, (int)EliteDrumsAction.EliteLeftCrash) },
            { ControlStrings.ELITE_DRUMS_TOM_1,         new(ControlStrings.ELITE_DRUMS_TOM_1,           BindingType.DrumButton, (int)EliteDrumsAction.EliteTom1) },
            { ControlStrings.ELITE_DRUMS_TOM_2,         new(ControlStrings.ELITE_DRUMS_TOM_2,           BindingType.DrumButton, (int)EliteDrumsAction.EliteTom2) },
            { ControlStrings.ELITE_DRUMS_TOM_3,         new(ControlStrings.ELITE_DRUMS_TOM_3,           BindingType.DrumButton, (int)EliteDrumsAction.EliteTom3) },
            { ControlStrings.ELITE_DRUMS_RIDE,          new(ControlStrings.ELITE_DRUMS_RIDE,            BindingType.DrumButton, (int)EliteDrumsAction.EliteRide) },
            { ControlStrings.ELITE_DRUMS_RIGHT_CRASH,   new(ControlStrings.ELITE_DRUMS_RIGHT_CRASH,     BindingType.DrumButton, (int)EliteDrumsAction.EliteRightCrash) },

            { ControlStrings.ELITE_DRUMS_4L_RED,        new(ControlStrings.ELITE_DRUMS_4L_RED,          BindingType.DrumButton, (int)EliteDrumsAction.FourLaneRedDrum) },
            { ControlStrings.ELITE_DRUMS_4L_YTOM,       new(ControlStrings.ELITE_DRUMS_4L_YTOM,         BindingType.DrumButton, (int)EliteDrumsAction.FourLaneYellowDrum) },
            { ControlStrings.ELITE_DRUMS_4L_BTOM,       new(ControlStrings.ELITE_DRUMS_4L_BTOM,         BindingType.DrumButton, (int)EliteDrumsAction.FourLaneBlueDrum) },
            { ControlStrings.ELITE_DRUMS_4L_GTOM,       new(ControlStrings.ELITE_DRUMS_4L_GTOM,         BindingType.DrumButton, (int)EliteDrumsAction.FourLaneGreenDrum) },
            { ControlStrings.ELITE_DRUMS_4L_YCYM,       new(ControlStrings.ELITE_DRUMS_4L_YCYM,         BindingType.DrumButton, (int)EliteDrumsAction.FourLaneYellowCymbal) },
            { ControlStrings.ELITE_DRUMS_4L_BCYM,       new(ControlStrings.ELITE_DRUMS_4L_BCYM,         BindingType.DrumButton, (int)EliteDrumsAction.FourLaneBlueCymbal) },
            { ControlStrings.ELITE_DRUMS_4L_GCYM,       new(ControlStrings.ELITE_DRUMS_4L_GCYM,         BindingType.DrumButton, (int)EliteDrumsAction.FourLaneGreenCymbal) },

            { ControlStrings.ELITE_DRUMS_5L_RED,        new(ControlStrings.ELITE_DRUMS_5L_RED,          BindingType.DrumButton, (int)EliteDrumsAction.FiveLaneRedDrum) },
            { ControlStrings.ELITE_DRUMS_5L_BLUE,       new(ControlStrings.ELITE_DRUMS_5L_BLUE,         BindingType.DrumButton, (int)EliteDrumsAction.FiveLaneBlueDrum) },
            { ControlStrings.ELITE_DRUMS_5L_GREEN,      new(ControlStrings.ELITE_DRUMS_5L_GREEN,        BindingType.DrumButton, (int)EliteDrumsAction.FiveLaneGreenDrum) },
            { ControlStrings.ELITE_DRUMS_5L_YELLOW,     new(ControlStrings.ELITE_DRUMS_5L_YELLOW,       BindingType.DrumButton, (int)EliteDrumsAction.FiveLaneYellowCymbal) },
            { ControlStrings.ELITE_DRUMS_5L_ORANGE,     new(ControlStrings.ELITE_DRUMS_5L_ORANGE,       BindingType.DrumButton, (int)EliteDrumsAction.FiveLaneOrangeCymbal) },
        };
    }
}
