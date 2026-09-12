using System;
using System.Collections.Generic;
using System.Linq;
using YARG.Core;
using YARG.Menu.ProfileList;

namespace YARG.Input.Bindings
{
    // Source: https://www.alesis.com/rscdn/1886/documents/Nitro%20Drum%20Module%20-%20User%20Guide%20-%20v1.2.pdf
    // Appendix, page 38

    public static partial class ReusableBindingSetDefaults
    {
        private static ReusableSingleButtonBindingConfig ALESIS_NITRO_KICK = new(ControllerFamily.MidiDevice, "note036"); // "Kick"

        private static List<ReusableSingleButtonBindingConfig> ALESIS_NITRO_SNARES = new()
        {
            new (ControllerFamily.MidiDevice, "note038"), // "Snare"
            new (ControllerFamily.MidiDevice, "note040"), // "Snare Rim"
        };

        private static ReusableSingleButtonBindingConfig ALESIS_NITRO_CLOSED_HAT = new(ControllerFamily.MidiDevice, "note042"); // "Hi-Hat Closed"
        private static ReusableSingleButtonBindingConfig ALESIS_NITRO_SIZZLE_HAT = new(ControllerFamily.MidiDevice, "note023"); // "Hi-Hat Half-Open"
        private static ReusableSingleButtonBindingConfig ALESIS_NITRO_OPEN_HAT = new(ControllerFamily.MidiDevice, "note046"); // "Hi-Hat Open"
        private static List<ReusableSingleButtonBindingConfig> ALESIS_NITRO_HI_HATS = new()
        {
            ALESIS_NITRO_CLOSED_HAT,
            ALESIS_NITRO_SIZZLE_HAT,
            ALESIS_NITRO_OPEN_HAT,
        };

        private static ReusableSingleButtonBindingConfig ALESIS_NITRO_STOMP = new(ControllerFamily.MidiDevice, "note044"); // "Hi-Hat Pedal"
        private static ReusableSingleButtonBindingConfig ALESIS_NITRO_SPLASH = new(ControllerFamily.MidiDevice, "note021"); // "Splash"

        private static ReusableSingleButtonBindingConfig ALESIS_NITRO_LEFT_CRASH = new(ControllerFamily.MidiDevice, "note049"); // "Crash 1"
        private static ReusableSingleButtonBindingConfig ALESIS_NITRO_RIGHT_CRASH = new(ControllerFamily.MidiDevice, "note057"); // "Crash 2"
        private static List<ReusableSingleButtonBindingConfig> ALESIS_NITRO_CRASHES = new()
        {
            ALESIS_NITRO_LEFT_CRASH,
            ALESIS_NITRO_RIGHT_CRASH,
        };  

        private static List<ReusableSingleButtonBindingConfig> ALESIS_NITRO_TOM_1S = new() {
            new (ControllerFamily.MidiDevice, "note048"), // "Tom 1"
            new (ControllerFamily.MidiDevice, "note050"), // "Tom 1 Rim"
        };

        private static List<ReusableSingleButtonBindingConfig> ALESIS_NITRO_TOM_2S = new() {
            new (ControllerFamily.MidiDevice, "note045"), // "Tom 2"
            new (ControllerFamily.MidiDevice, "note047"), // "Tom 2 Rim"
        };

        private static List<ReusableSingleButtonBindingConfig> ALESIS_NITRO_TOM_3S = new() {
            new (ControllerFamily.MidiDevice, "note039"), // "Tom 4 Rim"
            new (ControllerFamily.MidiDevice, "note041"), // "Tom 4"
            new (ControllerFamily.MidiDevice, "note043"), // "Tom 3"
            new (ControllerFamily.MidiDevice, "note058"), // "Tom 3 Rim"
        };

        private static List<ReusableSingleButtonBindingConfig> ALESIS_NITRO_RIDE = new() {
            new(ControllerFamily.MidiDevice, "note053"), // "Ride"
        };


        private static Dictionary<string, ReusableControlBinding> _alesisNitroDefaults = new()
        {
            {
                ControlStrings.DRUMS_KICK,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ControlStrings.DRUMS_KICK],
                    ALESIS_NITRO_KICK
                )
            },

            {
                ControlStrings.ELITE_DRUMS_STOMP,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ControlStrings.ELITE_DRUMS_STOMP],
                    ALESIS_NITRO_STOMP
                )
            },
            {
                ControlStrings.ELITE_DRUMS_SPLASH,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ControlStrings.ELITE_DRUMS_SPLASH],
                    ALESIS_NITRO_SPLASH
                )
            },

            {
                ControlStrings.ELITE_DRUMS_SNARE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ControlStrings.ELITE_DRUMS_SNARE],
                    ALESIS_NITRO_SNARES
                )
            },
            {
                ControlStrings.ELITE_DRUMS_CLOSED_HI_HAT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ControlStrings.ELITE_DRUMS_CLOSED_HI_HAT],
                    ALESIS_NITRO_CLOSED_HAT
                )
            },
            {
                ControlStrings.ELITE_DRUMS_SIZZLE_HI_HAT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ControlStrings.ELITE_DRUMS_SIZZLE_HI_HAT],
                    ALESIS_NITRO_SIZZLE_HAT
                )
            },
            {
                ControlStrings.ELITE_DRUMS_OPEN_HI_HAT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ControlStrings.ELITE_DRUMS_OPEN_HI_HAT],
                    ALESIS_NITRO_OPEN_HAT
                )
            },
            {
                ControlStrings.ELITE_DRUMS_LEFT_CRASH,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ControlStrings.ELITE_DRUMS_LEFT_CRASH],
                    ALESIS_NITRO_LEFT_CRASH
                )
            },
            {
                ControlStrings.ELITE_DRUMS_TOM_1,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ControlStrings.ELITE_DRUMS_TOM_1],
                    ALESIS_NITRO_TOM_1S
                )
            },
            {
                ControlStrings.ELITE_DRUMS_TOM_2,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ControlStrings.ELITE_DRUMS_TOM_2],
                    ALESIS_NITRO_TOM_2S
                )
            },
            {
                ControlStrings.ELITE_DRUMS_TOM_3,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ControlStrings.ELITE_DRUMS_TOM_3],
                    ALESIS_NITRO_TOM_3S
                )
            },
            {
                ControlStrings.ELITE_DRUMS_RIDE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ControlStrings.ELITE_DRUMS_RIDE],
                    ALESIS_NITRO_RIDE
                )
            },
            {
                ControlStrings.ELITE_DRUMS_RIGHT_CRASH,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ControlStrings.ELITE_DRUMS_RIGHT_CRASH],
                    ALESIS_NITRO_RIGHT_CRASH
                )
            },
            {
                ControlStrings.ELITE_DRUMS_4L_RED,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ControlStrings.ELITE_DRUMS_4L_RED],
                    ALESIS_NITRO_SNARES
                )
            },
            {
                ControlStrings.ELITE_DRUMS_4L_YTOM,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ControlStrings.ELITE_DRUMS_4L_YTOM],
                    ALESIS_NITRO_TOM_1S
                )
            },
            {
                ControlStrings.ELITE_DRUMS_4L_BTOM,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ControlStrings.ELITE_DRUMS_4L_BTOM],
                    ALESIS_NITRO_TOM_2S
                )
            },
            {
                ControlStrings.ELITE_DRUMS_4L_GTOM,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ControlStrings.ELITE_DRUMS_4L_GTOM],
                    ALESIS_NITRO_TOM_3S
                )
            },
            {
                ControlStrings.ELITE_DRUMS_4L_YCYM,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ControlStrings.ELITE_DRUMS_4L_YCYM],
                    ALESIS_NITRO_HI_HATS
                )
            },
            {
                ControlStrings.ELITE_DRUMS_4L_BCYM,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ControlStrings.ELITE_DRUMS_4L_BCYM],
                    ALESIS_NITRO_RIDE
                )
            },
            {
                ControlStrings.ELITE_DRUMS_4L_GCYM,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ControlStrings.ELITE_DRUMS_4L_GCYM],
                    ALESIS_NITRO_CRASHES
                )
            },
            {
                ControlStrings.ELITE_DRUMS_5L_RED,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ControlStrings.ELITE_DRUMS_5L_RED],
                    ALESIS_NITRO_SNARES
                )
            },
            {
                ControlStrings.ELITE_DRUMS_5L_BLUE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ControlStrings.ELITE_DRUMS_5L_BLUE],
                    ALESIS_NITRO_TOM_1S.Concat(ALESIS_NITRO_TOM_2S).ToList()
                )
            },
            {
                ControlStrings.ELITE_DRUMS_5L_GREEN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ControlStrings.ELITE_DRUMS_5L_GREEN],
                    ALESIS_NITRO_TOM_3S
                )
            },
            {
                ControlStrings.ELITE_DRUMS_5L_YELLOW,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ControlStrings.ELITE_DRUMS_5L_YELLOW],
                    ALESIS_NITRO_HI_HATS
                )
            },
            {
                ControlStrings.ELITE_DRUMS_5L_ORANGE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ControlStrings.ELITE_DRUMS_5L_ORANGE],
                    ALESIS_NITRO_CRASHES.Concat(ALESIS_NITRO_RIDE).ToList()
                )
            },
        };

        public static ReusableBindingSet AlesisNitroDrumkit = MakeHardcodedBindingSet(
            "Alesis Nitro/Surge",
            GameMode.EliteDrums,
            ControllerFamily.MidiDevice,
            _alesisNitroDefaults
        );
    }
}
