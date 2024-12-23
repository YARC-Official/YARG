using YARG.Core.IO;

namespace YARG.Core.Song
{
    public static class Midi_ProKeys_Preparser
    {
        private const int PROKEYS_MIN = 48;
        private const int PROKEYS_MAX = 72;
        private const int NOTES_IN_DIFFICULTY = PROKEYS_MAX - PROKEYS_MIN + 1;

        public static unsafe bool Parse(YARGMidiTrack track)
        {
            int statusBitMask = 0;
            var note = default(MidiNote);
            while (track.ParseEvent())
            {
                if (track.Type is MidiEventType.Note_On or MidiEventType.Note_Off)
                {
                    track.ExtractMidiNote(ref note);
                    if (PROKEYS_MIN <= note.value && note.value <= PROKEYS_MAX)
                    {
                        int statusMask = 1 << (note.value - PROKEYS_MIN);
                        if (track.Type == MidiEventType.Note_On && note.velocity > 0)
                        {
                            statusBitMask |= statusMask;
                        }
                        else if ((statusBitMask & statusMask) > 0)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }
    }
}
