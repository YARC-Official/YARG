using System;
using System.Text;
using YARG.Core.IO;

namespace YARG.Core.Song
{
    /// <remarks>
    /// Note: also functions as the five lane Keys preparser
    /// </remarks>
    public static class Midi_FiveFret_Preparser
    {
        private const int FIVEFRET_MIN = 59;
        // Open note included
        private const int NUM_LANES = 6;
        private const int SYSEX_DIFFICULTY_INDEX = 4;
        private const int SYSEX_TYPE_INDEX = 5;
        private const int SYSEX_STATUS_INDEX = 6;
        private const int OPEN_NOTE_TYPE = 1;
        private const int SYSEX_ALL_DIFFICULTIES = 0xFF;
        private const int GREEN_INDEX = 1;

        private static readonly byte[][] ENHANCED_STRINGS = new byte[][] { Encoding.ASCII.GetBytes("[ENHANCED_OPENS]"), Encoding.ASCII.GetBytes("ENHANCED_OPENS") };

        public static unsafe DifficultyMask Parse(YARGMidiTrack track)
        {
            ReadOnlySpan<byte> SYSEXTAG = stackalloc byte[] { (byte) 'P', (byte) 'S', (byte) '\0', };
            var validations = default(DifficultyMask);
            int statusBitMask = 0;

            // Zero is reserved for open notes. Open notes apply in two situations:
            // 1. The 13s will swap to zeroes when the ENHANCED_OPENS toggle occurs
            // 2. The '1'(green) in a difficulty will swap to zero and back depending on the Open note sysex state
            //
            // Note: the 13s account for the -1 offset of the minimum note value
            var indices = stackalloc int[MidiPreparser_Constants.NUM_DIFFICULTIES * MidiPreparser_Constants.NOTES_PER_DIFFICULTY]
            {
                13, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11,
                13, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11,
                13, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11,
                13, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11,
            };

            var note = default(MidiNote);
            while (track.ParseEvent())
            {
                if (track.Type is MidiEventType.Note_On or MidiEventType.Note_Off)
                {
                    track.ExtractMidiNote(ref note);
                    if (note.value < FIVEFRET_MIN || note.value > MidiPreparser_Constants.DEFAULT_MAX)
                    {
                        continue;
                    }

                    int noteOffset = note.value - FIVEFRET_MIN;
                    int diffIndex = MidiPreparser_Constants.DIFF_INDICES[noteOffset];
                    int laneIndex = indices[noteOffset];
                    var diffMask = (DifficultyMask) (1 << (diffIndex + 1));
                    if ((validations & diffMask) > 0 || laneIndex >= NUM_LANES)
                    {
                        continue;
                    }

                    int statusMask = 1 << (diffIndex * NUM_LANES + laneIndex);
                    // Note Ons with no velocity equates to a note Off by spec
                    if (track.Type == MidiEventType.Note_On && note.velocity > 0)
                    {
                        statusBitMask |= statusMask;
                    }
                    // Note off here
                    else if ((statusBitMask & statusMask) > 0)
                    {
                        validations |= diffMask;
                        if (validations == MidiPreparser_Constants.ALL_DIFFICULTIES)
                        {
                            break;
                        }
                    }
                }
                else if (track.Type is MidiEventType.SysEx or MidiEventType.SysEx_End)
                {
                    var str = track.ExtractTextOrSysEx();
                    if (str.StartsWith(SYSEXTAG) && str[SYSEX_TYPE_INDEX] == OPEN_NOTE_TYPE)
                    {
                        // 1 = GREEN; 0 = OPEN
                        int status = str[SYSEX_STATUS_INDEX] == 0 ? 1 : 0;
                        if (str[SYSEX_DIFFICULTY_INDEX] == SYSEX_ALL_DIFFICULTIES)
                        {
                            for (int diff = 0; diff < MidiPreparser_Constants.NUM_DIFFICULTIES; ++diff)
                            {
                                indices[MidiPreparser_Constants.NOTES_PER_DIFFICULTY * diff + GREEN_INDEX] = status;
                            }
                        }
                        else
                        {
                            indices[MidiPreparser_Constants.NOTES_PER_DIFFICULTY * str[SYSEX_DIFFICULTY_INDEX] + GREEN_INDEX] = status;
                        }
                    }
                }
                else if (MidiEventType.Text <= track.Type && track.Type <= MidiEventType.Text_EnumLimit)
                {
                    var str = track.ExtractTextOrSysEx();
                    if (str.SequenceEqual(ENHANCED_STRINGS[0]) || str.SequenceEqual(ENHANCED_STRINGS[1]))
                    {
                        for (int diff = 0; diff < MidiPreparser_Constants.NUM_DIFFICULTIES; ++diff)
                        {
                            indices[MidiPreparser_Constants.NOTES_PER_DIFFICULTY * diff] = 0;
                        }
                    }
                }
            }
            return validations;
        }
    }
}
