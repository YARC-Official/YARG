namespace YARG.Core.Chart
{
    public static class VocalsPartExtensions
    {
        public static void ConvertAllToUnpitched(this VocalsPart vocalsTrack)
        {
            for (int i = 0; i < vocalsTrack.NotePhrases.Count; i++)
            {
                var phrase = vocalsTrack.NotePhrases[i];
                var phraseParent = phrase.PhraseParentNote;

                // Create a new phrase based off of the original one
                var newPhraseParent = new VocalNote(phraseParent.Flags, phraseParent.Time,
                    phraseParent.TimeLength, phraseParent.Tick, phraseParent.TickLength);

                foreach (var note in phraseParent.ChildNotes)
                {
                    if (note.Type == VocalNoteType.Percussion)
                    {
                        continue;
                    }

                    // Create an unpitched replacement note. Make sure to use the total lengths instead
                    // of the normal lengths.
                    var newNote = new VocalNote(-1f, note.HarmonyPart, note.Type, note.Time,
                        note.TotalTimeLength, note.Tick, note.TotalTickLength);
                    newPhraseParent.AddChildNote(newNote);
                }

                // Replace the next and previous note values
                if (i - 1 >= 0)
                {
                    var lastPhrase = vocalsTrack.NotePhrases[i - 1];
                    newPhraseParent.PreviousNote = lastPhrase.PhraseParentNote;
                    lastPhrase.PhraseParentNote.NextNote = newPhraseParent;
                }

                // Replace the phrase
                var newPhrase = new VocalsPhrase(phrase.Time, phrase.TimeLength, phrase.Tick, phrase.TickLength,
                    newPhraseParent, phrase.Lyrics);
                vocalsTrack.NotePhrases[i] = newPhrase;
            }
        }
    }
}