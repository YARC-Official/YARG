using System;
using System.Collections.Generic;
using System.Text;
using YARG.Core;
using YARG.Helpers;
using YARG.Menu.ProfileList;

namespace YARG.Input.Bindings
{
    public static partial class ReusableBindingSetDefaults
    {
        private static readonly Dictionary<string, ReusableControlBinding> _defaultGamepadMidiDrumkitGameplay = new()
        {
            {
                ActionStrings.DRUMS_KICK,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ActionStrings.DRUMS_KICK],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_LEFT_SHOULDER),
                        new(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_LEFT_TRIGGER),
                    }
                )
            },

            {
                ActionStrings.ELITE_DRUMS_4L_RED,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ActionStrings.ELITE_DRUMS_4L_RED],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_BUTTON_EAST)
                )
            },
            {
                ActionStrings.ELITE_DRUMS_4L_YTOM,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ActionStrings.ELITE_DRUMS_4L_YTOM],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_BUTTON_NORTH)
                )
            },
            {
                ActionStrings.ELITE_DRUMS_4L_BTOM,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ActionStrings.ELITE_DRUMS_4L_BTOM],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_BUTTON_WEST)
                )
            },
            {
                ActionStrings.ELITE_DRUMS_4L_GTOM,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ActionStrings.ELITE_DRUMS_4L_GTOM],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_BUTTON_SOUTH)
                )
            },

            {
                ActionStrings.ELITE_DRUMS_4L_YCYM,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ActionStrings.ELITE_DRUMS_4L_YCYM],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_LEFT_STICK_PRESS)
                )
            },
            {
                ActionStrings.ELITE_DRUMS_4L_BCYM,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ActionStrings.ELITE_DRUMS_4L_BCYM],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_RIGHT_STICK_PRESS)
                )
            },
            {
                ActionStrings.ELITE_DRUMS_4L_GCYM,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ActionStrings.ELITE_DRUMS_4L_GCYM],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_RIGHT_SHOULDER)
                )
            },

            {
                ActionStrings.ELITE_DRUMS_5L_RED,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ActionStrings.ELITE_DRUMS_5L_RED],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_BUTTON_EAST)
                )
            },

            {
                ActionStrings.ELITE_DRUMS_5L_YELLOW,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ActionStrings.ELITE_DRUMS_5L_YELLOW],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_BUTTON_NORTH)
                )
            },

            {
                ActionStrings.ELITE_DRUMS_5L_BLUE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ActionStrings.ELITE_DRUMS_5L_BLUE],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_BUTTON_WEST)
                )
            },

            {
                ActionStrings.ELITE_DRUMS_5L_ORANGE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ActionStrings.ELITE_DRUMS_5L_ORANGE],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_RIGHT_SHOULDER)
                )
            },

            {
                ActionStrings.ELITE_DRUMS_5L_GREEN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ActionStrings.ELITE_DRUMS_5L_GREEN],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_BUTTON_SOUTH)
                )
            },
        };

        public static readonly Guid GUID_GAMEPAD_AS_MIDI_DRUMKIT = new("cbb9a126-4e1e-41bb-9c6b-dc16d85f9367");
        public static readonly ReusableBindingSet DefaultGamepadMidiDrumkitGameplay = MakeHardcodedBindingSet(
            "Default Gamepad as MIDI Drumkit",
            GameMode.EliteDrums,
            ControllerFamily.Gamepad,
            GUID_GAMEPAD_AS_MIDI_DRUMKIT,
            _defaultGamepadMidiDrumkitGameplay
        );
    }
}
