using System;
using System.Collections.Generic;
using System.Text;
using YARG.Helpers;
using YARG.Core.Input;

namespace YARG.Input.Bindings
{
    public static partial class ReusableBindingSetTemplates
    {
        public static Dictionary<string, InputActionInfo> FIVE_LANE_DRUMKIT = new()
        {
            { ActionStrings.DRUMS_KICK,                 new(ActionStrings.DRUMS_KICK,               BindingType.DrumButton, (int) DrumsAction.Kick) },

            { ActionStrings.FIVE_DRUMS_RED_PAD,         new(ActionStrings.FIVE_DRUMS_RED_PAD,       BindingType.DrumButton, (int) DrumsAction.RedDrum) },
            { ActionStrings.FIVE_DRUMS_YELLOW_CYMBAL,   new(ActionStrings.FIVE_DRUMS_YELLOW_CYMBAL, BindingType.DrumButton, (int) DrumsAction.YellowCymbal) },
            { ActionStrings.FIVE_DRUMS_BLUE_PAD,        new(ActionStrings.FIVE_DRUMS_BLUE_PAD,      BindingType.DrumButton, (int) DrumsAction.BlueDrum) },
            { ActionStrings.FIVE_DRUMS_ORANGE_CYMBAL,   new(ActionStrings.FIVE_DRUMS_ORANGE_CYMBAL, BindingType.DrumButton, (int) DrumsAction.OrangeCymbal) },
            { ActionStrings.FIVE_DRUMS_GREEN_PAD,       new(ActionStrings.FIVE_DRUMS_GREEN_PAD,     BindingType.DrumButton, (int) DrumsAction.GreenDrum) },
        };
    }
}
