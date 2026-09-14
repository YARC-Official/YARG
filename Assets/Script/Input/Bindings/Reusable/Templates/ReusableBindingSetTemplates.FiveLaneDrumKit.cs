using System;
using System.Collections.Generic;
using System.Text;
using YARG.Core.Input;

namespace YARG.Input.Bindings
{
    public static partial class ReusableBindingSetTemplates
    {
        public static Dictionary<string, InputActionInfo> FIVE_LANE_DRUMKIT = new()
        {
            { ControlStrings.DRUMS_KICK,        new(ControlStrings.DRUMS_KICK,          BindingType.DrumButton, (int) DrumsAction.Kick) },

            { ControlStrings.FIVE_DRUMS_RED_PAD,    new(ControlStrings.FIVE_DRUMS_RED_PAD,      BindingType.DrumButton, (int) DrumsAction.RedDrum) },
            { ControlStrings.FIVE_DRUMS_YELLOW_CYMBAL, new(ControlStrings.FIVE_DRUMS_YELLOW_CYMBAL,   BindingType.DrumButton, (int) DrumsAction.YellowCymbal) },
            { ControlStrings.FIVE_DRUMS_BLUE_PAD,   new(ControlStrings.FIVE_DRUMS_BLUE_PAD,     BindingType.DrumButton, (int) DrumsAction.BlueDrum) },
            { ControlStrings.FIVE_DRUMS_ORANGE_CYMBAL, new(ControlStrings.FIVE_DRUMS_ORANGE_CYMBAL,   BindingType.DrumButton, (int) DrumsAction.OrangeCymbal) },
            { ControlStrings.FIVE_DRUMS_GREEN_PAD,  new(ControlStrings.FIVE_DRUMS_GREEN_PAD,    BindingType.DrumButton, (int) DrumsAction.GreenDrum) },
        };
    }
}
