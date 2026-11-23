using System;
using System.Collections.Generic;
using System.Text;
using YARG.Assets.Script.Helpers;
using YARG.Core.Input;
using YARG.Helpers;

namespace YARG.Input.Bindings
{
    public static partial class ReusableBindingSetTemplates
    {
        public static Dictionary<string, InputActionInfo> FOUR_LANE_DRUMKIT = new()
        {
            { ActionStrings.DRUMS_KICK,                new(ActionStrings.DRUMS_KICK,                  BindingType.Impulse, (int) DrumsAction.Kick) },

            { ActionStrings.FOUR_DRUMS_RED_PAD,        new(ActionStrings.FOUR_DRUMS_RED_PAD,          BindingType.Impulse, (int) DrumsAction.RedDrum) },
            { ActionStrings.FOUR_DRUMS_YELLOW_PAD,     new(ActionStrings.FOUR_DRUMS_YELLOW_PAD,       BindingType.Impulse, (int) DrumsAction.YellowDrum) },
            { ActionStrings.FOUR_DRUMS_BLUE_PAD,       new(ActionStrings.FOUR_DRUMS_BLUE_PAD,         BindingType.Impulse, (int) DrumsAction.BlueDrum) },
            { ActionStrings.FOUR_DRUMS_GREEN_PAD,      new(ActionStrings.FOUR_DRUMS_GREEN_PAD,        BindingType.Impulse, (int) DrumsAction.GreenDrum) },

            { ActionStrings.FOUR_DRUMS_YELLOW_CYMBAL,  new(ActionStrings.FOUR_DRUMS_YELLOW_CYMBAL,    BindingType.Impulse, (int) DrumsAction.YellowCymbal) },
            { ActionStrings.FOUR_DRUMS_BLUE_CYMBAL,    new(ActionStrings.FOUR_DRUMS_BLUE_CYMBAL,      BindingType.Impulse, (int) DrumsAction.BlueCymbal) },
            { ActionStrings.FOUR_DRUMS_GREEN_CYMBAL,   new(ActionStrings.FOUR_DRUMS_GREEN_CYMBAL,     ActionStrings.FOUR_DRUMS_RED_CYMBAL,                       BindingType.Impulse, (int) DrumsAction.GreenCymbal) },
        };
    }
}
