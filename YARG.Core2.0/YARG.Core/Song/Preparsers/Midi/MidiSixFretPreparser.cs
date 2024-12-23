using YARG.Core.IO;

namespace YARG.Core.Song
{
    public static class Midi_SixFret_Preparser
    {
        private const int SIXFRET_MIN = 58;
        private const int SIXFRET_MAX = 103;
        // Open note included
        private const int NUM_LANES = 7;

        // Six fret indexing is fucked
        private static readonly int[] INDICES = new int[MidiPreparser_Constants.NUM_DIFFICULTIES * MidiPreparser_Constants.NOTES_PER_DIFFICULTY]
        {
            0, 4, 5, 6, 1, 2, 3, 7, 8, 9, 10, 11,
            0, 4, 5, 6, 1, 2, 3, 7, 8, 9, 10, 11,
            0, 4, 5, 6, 1, 2, 3, 7, 8, 9, 10, 11,
            0, 4, 5, 6, 1, 2, 3, 7, 8, 9, 10, 11,
        };

        public static unsafe DifficultyMask Parse(YARGMidiTrack track)
        {
            var validations = default(DifficultyMask);
            int statusBitMask = 0;

            var note = default(MidiNote);
            while (track.ParseEvent())
            {
                if (track.Type is MidiEventType.Note_On or MidiEventType.Note_Off)
                {
                    track.ExtractMidiNote(ref note);
                    if (note.value < SIXFRET_MIN || note.value > SIXFRET_MAX)
                    {
                        continue;
                    }

                    int noteOffset = note.value - SIXFRET_MIN;
                    int diffIndex = MidiPreparser_Constants.DIFF_INDICES[noteOffset];
                    int laneIndex = INDICES[noteOffset];
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
            }
            return validations;
        }
    }
}
