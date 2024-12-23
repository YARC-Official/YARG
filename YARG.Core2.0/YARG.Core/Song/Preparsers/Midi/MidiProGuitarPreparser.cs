using YARG.Core.IO;

namespace YARG.Core.Song
{
    public static class Midi_ProGuitar_Preparser
    {
        private const int NOTES_PER_DIFFICULTY = 24;
        private const int PROGUITAR_MAX = PROGUITAR_MIN + MidiPreparser_Constants.NUM_DIFFICULTIES * NOTES_PER_DIFFICULTY;
        private const int PROGUITAR_MIN = 24;
        private const int NUM_STRINGS = 6;
        private const int MIN_VELOCITY = 100;
        private const int ARPEGGIO_CHANNEL = 1;

        private const int MAXVELOCTIY_17 = 117;
        public static DifficultyMask Parse_17Fret(YARGMidiTrack track)
        {
            return Parse(track, MAXVELOCTIY_17);
        }

        private const int MAXVELOCITY_22 = 122;
        public static DifficultyMask Parse_22Fret(YARGMidiTrack track)
        {
            return Parse(track, MAXVELOCITY_22);
        }

        private static unsafe DifficultyMask Parse(YARGMidiTrack track, int maxVelocity)
        {
            var validations = default(DifficultyMask);
            int statusBitMask = 0;

            var note = default(MidiNote);
            while (track.ParseEvent())
            {
                if (track.Type is MidiEventType.Note_On or MidiEventType.Note_Off)
                {
                    track.ExtractMidiNote(ref note);
                    if (note.value < PROGUITAR_MIN || note.value > PROGUITAR_MAX)
                    {
                        continue;
                    }

                    int noteOffset = note.value - PROGUITAR_MIN;
                    int diffIndex = MidiPreparser_Constants.EXTENDED_DIFF_INDICES[noteOffset];
                    int laneIndex = MidiPreparser_Constants.EXTENDED_LANE_INDICES[noteOffset];
                    var diffMask = (DifficultyMask) (1 << (diffIndex + 1));
                    //                                                         Ghost notes aren't played
                    if ((validations & diffMask) > 0 || laneIndex >= NUM_STRINGS || track.Channel == ARPEGGIO_CHANNEL)
                    {
                        continue;
                    }

                    int statusMask = 1 << (diffIndex * NUM_STRINGS + laneIndex);
                    // Note Ons with no velocity equates to a note Off by spec
                    if (track.Type == MidiEventType.Note_On && note.velocity > 0)
                    {
                        if (MIN_VELOCITY <= note.velocity && note.velocity <= maxVelocity)
                        {
                            statusBitMask |= statusMask;
                        }
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
