using System.Collections.Generic;
using YARG.Core.Input;
using YARG.Helpers;

namespace YARG.Input.Bindings
{
    public static partial class ReusableBindingSetTemplates
    {
        public static Dictionary<string, InputActionInfo> KEYS = new()
        {
            { ActionStrings.KEYS_PRO_KEY_1,        new(ActionStrings.KEYS_PRO_KEY_1,          BindingType.Button,           (int)ProKeysAction.Key1) },
            { ActionStrings.KEYS_PRO_KEY_2,        new(ActionStrings.KEYS_PRO_KEY_2,          BindingType.Button,           (int)ProKeysAction.Key2) },
            { ActionStrings.KEYS_PRO_KEY_3,        new(ActionStrings.KEYS_PRO_KEY_3,          BindingType.Button,           (int)ProKeysAction.Key3) },
            { ActionStrings.KEYS_PRO_KEY_4,        new(ActionStrings.KEYS_PRO_KEY_4,          BindingType.Button,           (int)ProKeysAction.Key4) },
            { ActionStrings.KEYS_PRO_KEY_5,        new(ActionStrings.KEYS_PRO_KEY_5,          BindingType.Button,           (int)ProKeysAction.Key5) },
            { ActionStrings.KEYS_PRO_KEY_6,        new(ActionStrings.KEYS_PRO_KEY_6,          BindingType.Button,           (int)ProKeysAction.Key6) },
            { ActionStrings.KEYS_PRO_KEY_7,        new(ActionStrings.KEYS_PRO_KEY_7,          BindingType.Button,           (int)ProKeysAction.Key7) },
            { ActionStrings.KEYS_PRO_KEY_8,        new(ActionStrings.KEYS_PRO_KEY_8,          BindingType.Button,           (int)ProKeysAction.Key8) },
            { ActionStrings.KEYS_PRO_KEY_9,        new(ActionStrings.KEYS_PRO_KEY_9,          BindingType.Button,           (int)ProKeysAction.Key9) },
            { ActionStrings.KEYS_PRO_KEY_10,       new(ActionStrings.KEYS_PRO_KEY_10,         BindingType.Button,           (int)ProKeysAction.Key10) },
            { ActionStrings.KEYS_PRO_KEY_11,       new(ActionStrings.KEYS_PRO_KEY_11,         BindingType.Button,           (int)ProKeysAction.Key11) },
            { ActionStrings.KEYS_PRO_KEY_12,       new(ActionStrings.KEYS_PRO_KEY_12,         BindingType.Button,           (int)ProKeysAction.Key12) },
            { ActionStrings.KEYS_PRO_KEY_13,       new(ActionStrings.KEYS_PRO_KEY_13,         BindingType.Button,           (int)ProKeysAction.Key13) },
            { ActionStrings.KEYS_PRO_KEY_14,       new(ActionStrings.KEYS_PRO_KEY_14,         BindingType.Button,           (int)ProKeysAction.Key14) },
            { ActionStrings.KEYS_PRO_KEY_15,       new(ActionStrings.KEYS_PRO_KEY_15,         BindingType.Button,           (int)ProKeysAction.Key15) },
            { ActionStrings.KEYS_PRO_KEY_16,       new(ActionStrings.KEYS_PRO_KEY_16,         BindingType.Button,           (int)ProKeysAction.Key16) },
            { ActionStrings.KEYS_PRO_KEY_17,       new(ActionStrings.KEYS_PRO_KEY_17,         BindingType.Button,           (int)ProKeysAction.Key17) },
            { ActionStrings.KEYS_PRO_KEY_18,       new(ActionStrings.KEYS_PRO_KEY_18,         BindingType.Button,           (int)ProKeysAction.Key18) },
            { ActionStrings.KEYS_PRO_KEY_19,       new(ActionStrings.KEYS_PRO_KEY_19,         BindingType.Button,           (int)ProKeysAction.Key19) },
            { ActionStrings.KEYS_PRO_KEY_20,       new(ActionStrings.KEYS_PRO_KEY_20,         BindingType.Button,           (int)ProKeysAction.Key20) },
            { ActionStrings.KEYS_PRO_KEY_21,       new(ActionStrings.KEYS_PRO_KEY_21,         BindingType.Button,           (int)ProKeysAction.Key21) },
            { ActionStrings.KEYS_PRO_KEY_22,       new(ActionStrings.KEYS_PRO_KEY_22,         BindingType.Button,           (int)ProKeysAction.Key22) },
            { ActionStrings.KEYS_PRO_KEY_23,       new(ActionStrings.KEYS_PRO_KEY_23,         BindingType.Button,           (int)ProKeysAction.Key23) },
            { ActionStrings.KEYS_PRO_KEY_24,       new(ActionStrings.KEYS_PRO_KEY_24,         BindingType.Button,           (int)ProKeysAction.Key24) },
            { ActionStrings.KEYS_PRO_KEY_25,       new(ActionStrings.KEYS_PRO_KEY_25,         BindingType.Button,           (int)ProKeysAction.Key25) },

            { ActionStrings.KEYS_FIVE_LANE_OPEN,   new(ActionStrings.KEYS_FIVE_LANE_OPEN,     BindingType.Button,           (int)ProKeysAction.OpenNote) },
            { ActionStrings.KEYS_FIVE_LANE_GREEN,  new(ActionStrings.KEYS_FIVE_LANE_GREEN,    BindingType.Button,           (int)ProKeysAction.GreenKey) },
            { ActionStrings.KEYS_FIVE_LANE_RED,    new(ActionStrings.KEYS_FIVE_LANE_RED,      BindingType.Button,           (int)ProKeysAction.RedKey) },
            { ActionStrings.KEYS_FIVE_LANE_YELLOW, new(ActionStrings.KEYS_FIVE_LANE_YELLOW,   BindingType.Button,           (int)ProKeysAction.YellowKey) },
            { ActionStrings.KEYS_FIVE_LANE_BLUE,   new(ActionStrings.KEYS_FIVE_LANE_BLUE,     BindingType.Button,           (int)ProKeysAction.BlueKey) },
            { ActionStrings.KEYS_FIVE_LANE_ORANGE, new(ActionStrings.KEYS_FIVE_LANE_ORANGE,   BindingType.Button,           (int)ProKeysAction.OrangeKey) },

            { ActionStrings.KEYS_STAR_POWER,       new(ActionStrings.KEYS_STAR_POWER,         BindingType.Button,           (int)ProKeysAction.StarPower) },
            { ActionStrings.KEYS_TOUCH_EFFECTS,    new(ActionStrings.KEYS_TOUCH_EFFECTS,      BindingType.Axis,             (int)ProKeysAction.TouchEffects) },

        };
    }
}