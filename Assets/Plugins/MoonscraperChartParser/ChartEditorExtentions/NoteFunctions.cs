// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using System.Collections.Generic;
using MoonscraperChartEditor.Song;

public static class NoteFunctions {

    /// <summary>
    /// Gets all the notes (including this one) that share the same tick position as this one.
    /// </summary>
    /// <returns>Returns an array of all the notes currently sharing the same tick position as this note.</returns>
    public static MoonNote[] GetChord(this MoonNote moonNote)
    {
        List<MoonNote> chord = new List<MoonNote>();
        chord.Add(moonNote);
    
        MoonNote previous = moonNote.previous;
        while (previous != null && previous.tick == moonNote.tick)
        {
            chord.Add(previous);
            previous = previous.previous;
        }
    
        MoonNote next = moonNote.next;
        while (next != null && next.tick == moonNote.tick)
        {
            chord.Add(next);
            next = next.next;
        }
    
        return chord.ToArray();
    }

    public static void ApplyFlagsToChord(this MoonNote moonNote)
    {
        foreach (MoonNote chordNote in moonNote.chord)
        {
            chordNote.flags = CopyChordFlags(chordNote.flags, moonNote.flags);
        }
    }
    
    static MoonNote.Flags CopyChordFlags(MoonNote.Flags original, MoonNote.Flags noteToCopyFrom)
    {
        MoonNote.Flags flagsToPreserve = original & MoonNote.PER_NOTE_FLAGS;
        MoonNote.Flags newFlags = noteToCopyFrom & ~MoonNote.PER_NOTE_FLAGS;
        newFlags |= flagsToPreserve;

        return newFlags;
    }
}
