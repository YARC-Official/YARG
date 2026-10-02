using System;
using System.Collections.Generic;
using YARG.Core;
using YARG.Menu.ProfileList;

namespace YARG.Input.Bindings
{
    public static partial class ReusableBindingSetDefaults
    {
        private const int MIN_MIDI_NOTE = 0;
        private const int MAX_MIDI_NOTE = 127;
        private const int MIDI_MIDDLE_C = 60;
        private const int MIDI_NOTES_IN_OCTAVE = 12;
        private const int PRO_KEYS_OCTAVE_INTERVAL = 3; // Pro Keys bindings span 3 octaves, so loop every 3

        private static List<ReusableSingleButtonBindingConfig> GenerateOctaveRepetitions(int note, int interval = 1)
        {
            if (note is < MIN_MIDI_NOTE or > MAX_MIDI_NOTE)
            {
                throw new ArgumentOutOfRangeException($"{note} is outside MIDI note range");
            }


            static string NoteNumberPath(int num) => $"note{num.ToString("D3")}";

            List<ReusableSingleButtonBindingConfig> bindingConfigs = new()
            {
                new(ControllerFamily.MidiDevice, NoteNumberPath(note))
            };

            var down = note - (MIDI_NOTES_IN_OCTAVE * interval);
            while (down > MIN_MIDI_NOTE)
            {
                bindingConfigs.Add(new(ControllerFamily.MidiDevice, NoteNumberPath(down)));
                down -= MIDI_NOTES_IN_OCTAVE * interval;
            }

            var up = note + (MIDI_NOTES_IN_OCTAVE * interval);
            while (up < MAX_MIDI_NOTE)
            {
                bindingConfigs.Add(new(ControllerFamily.MidiDevice, NoteNumberPath(up)));
                up += MIDI_NOTES_IN_OCTAVE * interval;
            }

            return bindingConfigs;
        }


        private static Dictionary<string, ReusableControlBinding> _midiKeyboardDefaults = new()
        {
            {
                ControlStrings.KEYS_PRO_KEY_1,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_1],
                    GenerateOctaveRepetitions(MIDI_MIDDLE_C, PRO_KEYS_OCTAVE_INTERVAL)
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_2,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_2],
                    GenerateOctaveRepetitions(MIDI_MIDDLE_C + 1, PRO_KEYS_OCTAVE_INTERVAL)
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_3,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_3],
                    GenerateOctaveRepetitions(MIDI_MIDDLE_C + 2, PRO_KEYS_OCTAVE_INTERVAL)
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_4,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_4],
                    GenerateOctaveRepetitions(MIDI_MIDDLE_C + 3, PRO_KEYS_OCTAVE_INTERVAL)
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_5,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_5],
                    GenerateOctaveRepetitions(MIDI_MIDDLE_C + 4, PRO_KEYS_OCTAVE_INTERVAL)
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_6,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_6],
                    GenerateOctaveRepetitions(MIDI_MIDDLE_C + 5, PRO_KEYS_OCTAVE_INTERVAL)
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_7,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_7],
                    GenerateOctaveRepetitions(MIDI_MIDDLE_C + 6, PRO_KEYS_OCTAVE_INTERVAL)
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_8,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_8],
                    GenerateOctaveRepetitions(MIDI_MIDDLE_C + 7, PRO_KEYS_OCTAVE_INTERVAL)
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_9,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_9],
                    GenerateOctaveRepetitions(MIDI_MIDDLE_C + 8, PRO_KEYS_OCTAVE_INTERVAL)
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_10,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_10],
                    GenerateOctaveRepetitions(MIDI_MIDDLE_C + 9, PRO_KEYS_OCTAVE_INTERVAL)
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_11,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_11],
                    GenerateOctaveRepetitions(MIDI_MIDDLE_C + 10, PRO_KEYS_OCTAVE_INTERVAL)
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_12,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_12],
                    GenerateOctaveRepetitions(MIDI_MIDDLE_C + 11, PRO_KEYS_OCTAVE_INTERVAL)
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_13,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_13],
                    GenerateOctaveRepetitions(MIDI_MIDDLE_C + 12, PRO_KEYS_OCTAVE_INTERVAL)
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_14,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_14],
                    GenerateOctaveRepetitions(MIDI_MIDDLE_C + 13, PRO_KEYS_OCTAVE_INTERVAL)
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_15,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_15],
                    GenerateOctaveRepetitions(MIDI_MIDDLE_C + 14, PRO_KEYS_OCTAVE_INTERVAL)
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_16,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_16],
                    GenerateOctaveRepetitions(MIDI_MIDDLE_C + 15, PRO_KEYS_OCTAVE_INTERVAL)
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_17,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_17],
                    GenerateOctaveRepetitions(MIDI_MIDDLE_C + 16, PRO_KEYS_OCTAVE_INTERVAL)
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_18,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_18],
                    GenerateOctaveRepetitions(MIDI_MIDDLE_C + 17, PRO_KEYS_OCTAVE_INTERVAL)
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_19,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_19],
                    GenerateOctaveRepetitions(MIDI_MIDDLE_C + 18, PRO_KEYS_OCTAVE_INTERVAL)
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_20,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_20],
                    GenerateOctaveRepetitions(MIDI_MIDDLE_C + 19, PRO_KEYS_OCTAVE_INTERVAL)
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_21,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_21],
                    GenerateOctaveRepetitions(MIDI_MIDDLE_C + 20, PRO_KEYS_OCTAVE_INTERVAL)
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_22,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_22],
                    GenerateOctaveRepetitions(MIDI_MIDDLE_C + 21, PRO_KEYS_OCTAVE_INTERVAL)
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_23,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_23],
                    GenerateOctaveRepetitions(MIDI_MIDDLE_C + 22, PRO_KEYS_OCTAVE_INTERVAL)
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_24,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_24],
                    GenerateOctaveRepetitions(MIDI_MIDDLE_C + 23, PRO_KEYS_OCTAVE_INTERVAL)
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_25,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_25],
                    GenerateOctaveRepetitions(MIDI_MIDDLE_C + 24, PRO_KEYS_OCTAVE_INTERVAL)
                )
            },

            {
                ControlStrings.KEYS_FIVE_LANE_OPEN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_FIVE_LANE_OPEN],
                    GenerateOctaveRepetitions(MIDI_MIDDLE_C - 1) // All Bs
                )
            },
            {
                ControlStrings.KEYS_FIVE_LANE_GREEN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_FIVE_LANE_GREEN],
                    GenerateOctaveRepetitions(MIDI_MIDDLE_C) // All Cs
                )
            },
            {
                ControlStrings.KEYS_FIVE_LANE_RED,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_FIVE_LANE_RED],
                    GenerateOctaveRepetitions(MIDI_MIDDLE_C + 2) // All Ds
                )
            },
            {
                ControlStrings.KEYS_FIVE_LANE_YELLOW,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_FIVE_LANE_YELLOW],
                    GenerateOctaveRepetitions(MIDI_MIDDLE_C + 4) // All Es
                )
            },
            {
                ControlStrings.KEYS_FIVE_LANE_BLUE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_FIVE_LANE_BLUE],
                    GenerateOctaveRepetitions(MIDI_MIDDLE_C + 5) // All Fs
                )
            },
            {
                ControlStrings.KEYS_FIVE_LANE_ORANGE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_FIVE_LANE_ORANGE],
                    GenerateOctaveRepetitions(MIDI_MIDDLE_C + 7) // All Gs
                )
            },

            {
                ControlStrings.KEYS_TOUCH_EFFECTS,
                new ReusableAxisBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_TOUCH_EFFECTS],
                    new ReusableSingleAxisBindingConfig(ControllerFamily.MidiDevice, "pitchBend")
                )
            },
        };

        public static ReusableBindingSet DefaultMidiKeyboard = MakeHardcodedBindingSet(
            "Default MIDI Keyboard",
            GameMode.ProKeys,
            ControllerFamily.MidiDevice,
            _midiKeyboardDefaults
        );
    }
}
