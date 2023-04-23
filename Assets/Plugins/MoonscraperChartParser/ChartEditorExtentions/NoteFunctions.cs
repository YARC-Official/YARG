// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using System.Collections.Generic;
using MoonscraperChartEditor.Song;

public static class NoteFunctions {

    /// <summary>
    /// Gets all the notes (including this one) that share the same tick position as this one.
    /// </summary>
    /// <returns>Returns an array of all the notes currently sharing the same tick position as this note.</returns>
    public static Note[] GetChord(this Note note)
    {
        List<Note> chord = new List<Note>();
        chord.Add(note);
    
        Note previous = note.previous;
        while (previous != null && previous.tick == note.tick)
        {
            chord.Add(previous);
            previous = previous.previous;
        }
    
        Note next = note.next;
        while (next != null && next.tick == note.tick)
        {
            chord.Add(next);
            next = next.next;
        }
    
        return chord.ToArray();
    }

    public static void ApplyFlagsToChord(this Note note)
    {
        foreach (Note chordNote in note.chord)
        {
            chordNote.flags = CopyChordFlags(chordNote.flags, note.flags);
        }
    }
    
    static Note.Flags CopyChordFlags(Note.Flags original, Note.Flags noteToCopyFrom)
    {
        Note.Flags flagsToPreserve = original & Note.PER_NOTE_FLAGS;
        Note.Flags newFlags = noteToCopyFrom & ~Note.PER_NOTE_FLAGS;
        newFlags |= flagsToPreserve;

        return newFlags;
    }
}
