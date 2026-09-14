using System.Collections.Generic;
using YARG.Core.Input;

namespace YARG.Input.Bindings
{
    public static partial class ReusableBindingSetTemplates
    {
        public static Dictionary<string, InputActionInfo> KEYS = new()
        {
            { ControlStrings.KEYS_PRO_KEY_1,        new(ControlStrings.KEYS_PRO_KEY_1,          BindingType.Button,             (int)ProKeysAction.Key1) },
            { ControlStrings.KEYS_PRO_KEY_2,        new(ControlStrings.KEYS_PRO_KEY_2,          BindingType.Button,             (int)ProKeysAction.Key2) },
            { ControlStrings.KEYS_PRO_KEY_3,        new(ControlStrings.KEYS_PRO_KEY_3,          BindingType.Button,             (int)ProKeysAction.Key3) },
            { ControlStrings.KEYS_PRO_KEY_4,        new(ControlStrings.KEYS_PRO_KEY_4,          BindingType.Button,             (int)ProKeysAction.Key4) },
            { ControlStrings.KEYS_PRO_KEY_5,        new(ControlStrings.KEYS_PRO_KEY_5,          BindingType.Button,             (int)ProKeysAction.Key5) },
            { ControlStrings.KEYS_PRO_KEY_6,        new(ControlStrings.KEYS_PRO_KEY_6,          BindingType.Button,             (int)ProKeysAction.Key6) },
            { ControlStrings.KEYS_PRO_KEY_7,        new(ControlStrings.KEYS_PRO_KEY_7,          BindingType.Button,             (int)ProKeysAction.Key7) },
            { ControlStrings.KEYS_PRO_KEY_8,        new(ControlStrings.KEYS_PRO_KEY_8,          BindingType.Button,             (int)ProKeysAction.Key8) },
            { ControlStrings.KEYS_PRO_KEY_9,        new(ControlStrings.KEYS_PRO_KEY_9,          BindingType.Button,             (int)ProKeysAction.Key9) },
            { ControlStrings.KEYS_PRO_KEY_10,       new(ControlStrings.KEYS_PRO_KEY_10,         BindingType.Button,             (int)ProKeysAction.Key10) },
            { ControlStrings.KEYS_PRO_KEY_11,       new(ControlStrings.KEYS_PRO_KEY_11,         BindingType.Button,             (int)ProKeysAction.Key11) },
            { ControlStrings.KEYS_PRO_KEY_12,       new(ControlStrings.KEYS_PRO_KEY_12,         BindingType.Button,             (int)ProKeysAction.Key12) },
            { ControlStrings.KEYS_PRO_KEY_13,       new(ControlStrings.KEYS_PRO_KEY_13,         BindingType.Button,             (int)ProKeysAction.Key13) },
            { ControlStrings.KEYS_PRO_KEY_14,       new(ControlStrings.KEYS_PRO_KEY_14,         BindingType.Button,             (int)ProKeysAction.Key14) },
            { ControlStrings.KEYS_PRO_KEY_15,       new(ControlStrings.KEYS_PRO_KEY_15,         BindingType.Button,             (int)ProKeysAction.Key15) },
            { ControlStrings.KEYS_PRO_KEY_16,       new(ControlStrings.KEYS_PRO_KEY_16,         BindingType.Button,             (int)ProKeysAction.Key16) },
            { ControlStrings.KEYS_PRO_KEY_17,       new(ControlStrings.KEYS_PRO_KEY_17,         BindingType.Button,             (int)ProKeysAction.Key17) },
            { ControlStrings.KEYS_PRO_KEY_18,       new(ControlStrings.KEYS_PRO_KEY_18,         BindingType.Button,             (int)ProKeysAction.Key18) },
            { ControlStrings.KEYS_PRO_KEY_19,       new(ControlStrings.KEYS_PRO_KEY_19,         BindingType.Button,             (int)ProKeysAction.Key19) },
            { ControlStrings.KEYS_PRO_KEY_20,       new(ControlStrings.KEYS_PRO_KEY_20,         BindingType.Button,             (int)ProKeysAction.Key20) },
            { ControlStrings.KEYS_PRO_KEY_21,       new(ControlStrings.KEYS_PRO_KEY_21,         BindingType.Button,             (int)ProKeysAction.Key21) },
            { ControlStrings.KEYS_PRO_KEY_22,       new(ControlStrings.KEYS_PRO_KEY_22,         BindingType.Button,             (int)ProKeysAction.Key22) },
            { ControlStrings.KEYS_PRO_KEY_23,       new(ControlStrings.KEYS_PRO_KEY_23,         BindingType.Button,             (int)ProKeysAction.Key23) },
            { ControlStrings.KEYS_PRO_KEY_24,       new(ControlStrings.KEYS_PRO_KEY_24,         BindingType.Button,             (int)ProKeysAction.Key24) },
            { ControlStrings.KEYS_PRO_KEY_25,       new(ControlStrings.KEYS_PRO_KEY_25,         BindingType.Button,             (int)ProKeysAction.Key25) },

            { ControlStrings.KEYS_FIVE_LANE_OPEN,   new(ControlStrings.KEYS_FIVE_LANE_OPEN,     BindingType.Button,             (int)ProKeysAction.OpenNote) },
            { ControlStrings.KEYS_FIVE_LANE_GREEN,  new(ControlStrings.KEYS_FIVE_LANE_GREEN,    BindingType.Button,             (int)ProKeysAction.GreenKey) },
            { ControlStrings.KEYS_FIVE_LANE_RED,    new(ControlStrings.KEYS_FIVE_LANE_RED,      BindingType.Button,             (int)ProKeysAction.RedKey) },
            { ControlStrings.KEYS_FIVE_LANE_YELLOW, new(ControlStrings.KEYS_FIVE_LANE_YELLOW,   BindingType.Button,             (int)ProKeysAction.YellowKey) },
            { ControlStrings.KEYS_FIVE_LANE_BLUE,   new(ControlStrings.KEYS_FIVE_LANE_BLUE,     BindingType.Button,             (int)ProKeysAction.BlueKey) },
            { ControlStrings.KEYS_FIVE_LANE_ORANGE, new(ControlStrings.KEYS_FIVE_LANE_ORANGE,   BindingType.Button,             (int)ProKeysAction.OrangeKey) },

            { ControlStrings.KEYS_STAR_POWER,       new(ControlStrings.KEYS_STAR_POWER,         BindingType.IndividualButton,   (int)ProKeysAction.StarPower) },
            { ControlStrings.KEYS_TOUCH_EFFECTS,    new(ControlStrings.KEYS_TOUCH_EFFECTS,      BindingType.Axis,               (int)ProKeysAction.TouchEffects) },

        };
    }
}