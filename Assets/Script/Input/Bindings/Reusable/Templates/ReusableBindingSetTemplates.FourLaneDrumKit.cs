using System;
using System.Collections.Generic;
using System.Text;
using YARG.Core.Input;

namespace YARG.Input.Bindings
{
    public static partial class ReusableBindingSetTemplates
    {
        public static Dictionary<string, InputActionInfo> FOUR_LANE_DRUMKIT = new()
        {
            { ControlStrings.DRUMS_KICK,                new(ControlStrings.DRUMS_KICK,                  BindingType.DrumButton, (int) DrumsAction.Kick) },

            { ControlStrings.FOUR_DRUMS_RED_PAD,        new(ControlStrings.FOUR_DRUMS_RED_PAD,          BindingType.DrumButton, (int) DrumsAction.RedDrum) },
            { ControlStrings.FOUR_DRUMS_YELLOW_PAD,     new(ControlStrings.FOUR_DRUMS_YELLOW_PAD,       BindingType.DrumButton, (int) DrumsAction.YellowDrum) },
            { ControlStrings.FOUR_DRUMS_BLUE_PAD,       new(ControlStrings.FOUR_DRUMS_BLUE_PAD,         BindingType.DrumButton, (int) DrumsAction.BlueDrum) },
            { ControlStrings.FOUR_DRUMS_GREEN_PAD,      new(ControlStrings.FOUR_DRUMS_GREEN_PAD,        BindingType.DrumButton, (int) DrumsAction.GreenDrum) },

            { ControlStrings.FOUR_DRUMS_YELLOW_CYMBAL,  new(ControlStrings.FOUR_DRUMS_YELLOW_CYMBAL,    BindingType.DrumButton, (int) DrumsAction.YellowCymbal) },
            { ControlStrings.FOUR_DRUMS_BLUE_CYMBAL,    new(ControlStrings.FOUR_DRUMS_BLUE_CYMBAL,      BindingType.DrumButton, (int) DrumsAction.BlueCymbal) },
            { ControlStrings.FOUR_DRUMS_GREEN_CYMBAL,   new(ControlStrings.FOUR_DRUMS_GREEN_CYMBAL,     BindingType.DrumButton, (int) DrumsAction.GreenCymbal) },
        };
    }
}
