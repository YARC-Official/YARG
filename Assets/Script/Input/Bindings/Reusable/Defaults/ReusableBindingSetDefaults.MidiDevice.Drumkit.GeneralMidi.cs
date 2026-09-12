using System;
using System.Collections.Generic;
using System.Linq;
using YARG.Core;
using YARG.Menu.ProfileList;

namespace YARG.Input.Bindings
{
    // TODO: Add more default binding sets for common ekits so they can be used out-of-the-box

    public static partial class ReusableBindingSetDefaults
    {
        private static List<ReusableSingleButtonBindingConfig> GENERAL_MIDI_KICKS = new()
        {
            new (ControllerFamily.MidiDevice, "note035"), // "Acoustic Bass Drum"
            new (ControllerFamily.MidiDevice, "note036"), // "Bass Drum"
        };

        private static List<ReusableSingleButtonBindingConfig> GENERAL_MIDI_SNARES = new()
        {
            new (ControllerFamily.MidiDevice, "note037"), // "Side Stick"
            new (ControllerFamily.MidiDevice, "note038"), // "Acoustic Snare"
            new (ControllerFamily.MidiDevice, "note040"), // "Electric Snare"
        };

        private static ReusableSingleButtonBindingConfig GENERAL_MIDI_CLOSED_HAT = new(ControllerFamily.MidiDevice, "note042"); // "Closed Hi-Hat"
        private static ReusableSingleButtonBindingConfig GENERAL_MIDI_STOMP = new(ControllerFamily.MidiDevice, "note044"); // "Pedal Hi-Hat"
        private static ReusableSingleButtonBindingConfig GENERAL_MIDI_OPEN_HAT = new(ControllerFamily.MidiDevice, "note046"); // "Open Hi-Hat"
        private static List<ReusableSingleButtonBindingConfig> GENERAL_MIDI_HI_HATS = new()
        {
            GENERAL_MIDI_CLOSED_HAT,
            GENERAL_MIDI_OPEN_HAT,
        };

        private static ReusableSingleButtonBindingConfig GENERAL_MIDI_CRASH_1 = new(ControllerFamily.MidiDevice, "note049"); // "Crash Cymbal 1"
        private static ReusableSingleButtonBindingConfig GENERAL_MIDI_CRASH_2 = new(ControllerFamily.MidiDevice, "note057"); // "Crash Cymbal 2"
        private static List<ReusableSingleButtonBindingConfig> GENERAL_MIDI_CRASHES = new()
        {
            GENERAL_MIDI_CRASH_1,
            GENERAL_MIDI_CRASH_2,
        };

        private static List<ReusableSingleButtonBindingConfig> GENERAL_MIDI_HIGH_TOMS = new() {
            new (ControllerFamily.MidiDevice, "note048"), // "High Mid Tom"
            new (ControllerFamily.MidiDevice, "note050"), // "High Tom"
        };

        private static List<ReusableSingleButtonBindingConfig> GENERAL_MIDI_LOW_TOMS = new() {
            new (ControllerFamily.MidiDevice, "note045"), // "Low Tom"
            new (ControllerFamily.MidiDevice, "note047"), // "Low Mid Tom"
        };

        private static List<ReusableSingleButtonBindingConfig> GENERAL_MIDI_FLOOR_TOMS = new() {
            new (ControllerFamily.MidiDevice, "note041"), // "Low Floor Tom"
            new (ControllerFamily.MidiDevice, "note043"), // "High Floor Tom"
        };

        private static List<ReusableSingleButtonBindingConfig> GENERAL_MIDI_RIDES = new()
        {
            new (ControllerFamily.MidiDevice, "note053"), // "Ride Bell"
            new (ControllerFamily.MidiDevice, "note059"), // "Ride Cymbal"
        };



        private static Dictionary<string, ReusableControlBinding> _generalMidiDrumDefaults = new()
        {
            {
                ControlStrings.DRUMS_KICK,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ControlStrings.DRUMS_KICK],
                    GENERAL_MIDI_KICKS
                )
            },

            {
                ControlStrings.ELITE_DRUMS_STOMP,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ControlStrings.ELITE_DRUMS_STOMP],
                    GENERAL_MIDI_STOMP
                )
            },
            // GM doesn't have a hi-hat splash mapping (no, Note 055 "Splash Cymbal" is not the same thing)
            {
                ControlStrings.ELITE_DRUMS_SNARE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ControlStrings.ELITE_DRUMS_SNARE],
                    GENERAL_MIDI_SNARES
                )
            },
            {
                ControlStrings.ELITE_DRUMS_CLOSED_HI_HAT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ControlStrings.ELITE_DRUMS_CLOSED_HI_HAT],
                    GENERAL_MIDI_CLOSED_HAT
                )
            },
            // GM doesn't have a hi-hat sizzle mapping
            {
                ControlStrings.ELITE_DRUMS_OPEN_HI_HAT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ControlStrings.ELITE_DRUMS_OPEN_HI_HAT],
                    GENERAL_MIDI_OPEN_HAT
                )
            },
            {
                ControlStrings.ELITE_DRUMS_LEFT_CRASH,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ControlStrings.ELITE_DRUMS_LEFT_CRASH],
                    GENERAL_MIDI_CRASH_1
                )
            },
            {
                ControlStrings.ELITE_DRUMS_TOM_1,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ControlStrings.ELITE_DRUMS_TOM_1],
                    GENERAL_MIDI_HIGH_TOMS
                )
            },
            {
                ControlStrings.ELITE_DRUMS_TOM_2,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ControlStrings.ELITE_DRUMS_TOM_2],
                    GENERAL_MIDI_LOW_TOMS
                )
            },
            {
                ControlStrings.ELITE_DRUMS_TOM_3,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ControlStrings.ELITE_DRUMS_TOM_3],
                    GENERAL_MIDI_FLOOR_TOMS
                )
            },
            {
                ControlStrings.ELITE_DRUMS_RIDE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ControlStrings.ELITE_DRUMS_RIDE],
                    GENERAL_MIDI_RIDES
                )
            },
            {
                ControlStrings.ELITE_DRUMS_RIGHT_CRASH,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ControlStrings.ELITE_DRUMS_RIGHT_CRASH],
                    GENERAL_MIDI_CRASH_2
                )
            },
            {
                ControlStrings.ELITE_DRUMS_4L_RED,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ControlStrings.ELITE_DRUMS_4L_RED],
                    GENERAL_MIDI_SNARES
                )
            },
            {
                ControlStrings.ELITE_DRUMS_4L_YTOM,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ControlStrings.ELITE_DRUMS_4L_YTOM],
                    GENERAL_MIDI_HIGH_TOMS
                )
            },
            {
                ControlStrings.ELITE_DRUMS_4L_BTOM,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ControlStrings.ELITE_DRUMS_4L_BTOM],
                    GENERAL_MIDI_LOW_TOMS
                )
            },
            {
                ControlStrings.ELITE_DRUMS_4L_GTOM,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ControlStrings.ELITE_DRUMS_4L_GTOM],
                    GENERAL_MIDI_FLOOR_TOMS
                )
            },
            {
                ControlStrings.ELITE_DRUMS_4L_YCYM,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ControlStrings.ELITE_DRUMS_4L_YCYM],
                    GENERAL_MIDI_HI_HATS
                )
            },
            {
                ControlStrings.ELITE_DRUMS_4L_BCYM,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ControlStrings.ELITE_DRUMS_4L_BCYM],
                    GENERAL_MIDI_RIDES
                )
            },
            {
                ControlStrings.ELITE_DRUMS_4L_GCYM,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ControlStrings.ELITE_DRUMS_4L_GCYM],
                    GENERAL_MIDI_CRASHES
                )
            },
            {
                ControlStrings.ELITE_DRUMS_5L_RED,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ControlStrings.ELITE_DRUMS_5L_RED],
                    GENERAL_MIDI_SNARES
                )
            },
            {
                ControlStrings.ELITE_DRUMS_5L_BLUE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ControlStrings.ELITE_DRUMS_5L_BLUE],
                    GENERAL_MIDI_HIGH_TOMS.Concat(GENERAL_MIDI_LOW_TOMS).ToList()
                )
            },
            {
                ControlStrings.ELITE_DRUMS_5L_GREEN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ControlStrings.ELITE_DRUMS_5L_GREEN],
                    GENERAL_MIDI_FLOOR_TOMS
                )
            },
            {
                ControlStrings.ELITE_DRUMS_5L_YELLOW,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ControlStrings.ELITE_DRUMS_5L_YELLOW],
                    GENERAL_MIDI_HI_HATS
                )
            },
            {
                ControlStrings.ELITE_DRUMS_5L_ORANGE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ControlStrings.ELITE_DRUMS_5L_ORANGE],
                    GENERAL_MIDI_CRASHES.Concat(GENERAL_MIDI_RIDES).ToList()
                )
            },
        };



        public static ReusableBindingSet GeneralMidiDrumkit = MakeHardcodedBindingSet(
            "General MIDI Drums",
            GameMode.EliteDrums,
            ControllerFamily.MidiDevice,
            _generalMidiDrumDefaults
        );
    }
}
