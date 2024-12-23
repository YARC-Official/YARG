using YARG.Core.IO;

namespace YARG.Core.Song
{
    public static class Midi_Vocal_Preparser
    {
        private const int VOCAL_MIN = 36;
        private const int VOCAL_MAX = 84;
        private const int PERCUSSION_NOTE = 96;

        private const int VOCAL_PHRASE_1 = 105;
        private const int VOCAL_PHRASE_2 = 106;

        public static bool Parse(YARGMidiTrack track, bool isLeadVocals)
        {
            long vocalPosition = -1;
            long phrasePosition = -1;
            long percussionPosition = -1;

            var note = default(MidiNote);
            while (track.ParseEvent())
            {
                if (track.Type is MidiEventType.Note_On or MidiEventType.Note_Off)
                {
                    track.ExtractMidiNote(ref note);
                    // Note Ons with no velocity equates to a note Off by spec
                    if (track.Type == MidiEventType.Note_On && note.velocity > 0)
                    {
                        if (VOCAL_MIN <= note.value && note.value <= VOCAL_MAX)
                        {
                            vocalPosition = track.Position;
                        }
                        else if (note.value == VOCAL_PHRASE_1 || note.value == VOCAL_PHRASE_2)
                        {
                            phrasePosition = int.MaxValue;
                        }
                        else if (note.value == PERCUSSION_NOTE && isLeadVocals)
                        {
                            percussionPosition = track.Position;
                        }
                    }
                    // NoteOff from this point
                    else if (VOCAL_MIN <= note.value && note.value <= VOCAL_MAX)
                    {
                        // HARM 2/3 do not use phrases defined in their own tracks to mark playable vocals
                        if (vocalPosition >= 0 && (track.Position <= phrasePosition || !isLeadVocals))
                        {
                            return true;
                        }
                        vocalPosition = -1;
                    }
                    else if (note.value == VOCAL_PHRASE_1 || note.value == VOCAL_PHRASE_2)
                    {
                        // Accounts for when a phrase ends at the same time as a vocal/precussion note but is in-file first
                        phrasePosition = track.Position;
                    }
                    else if (note.value == PERCUSSION_NOTE)
                    {
                        if (percussionPosition >= 0 && track.Position <= phrasePosition)
                        {
                            return true;
                        }
                        percussionPosition = -1;
                    }
                }
            }
            return false;
        }
    }
}
