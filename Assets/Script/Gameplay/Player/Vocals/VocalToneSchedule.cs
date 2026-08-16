using System;
using System.Collections.Generic;
using YARG.Core.Audio;
using YARG.Core.Chart;

namespace YARG.Gameplay.Player
{
    /// <summary>
    /// Flattens a vocals part into the pitch schedule rendered by a <see cref="ToneChannel"/>.
    /// </summary>
    /// <remarks>
    /// The audio backend interpolates linearly within a segment, so a note is emitted as a held
    /// segment followed by one sliding segment per pitch change. This reproduces
    /// <see cref="VocalNote.PitchAtSongTime"/> without the backend needing to understand vocals.
    /// </remarks>
    public static class VocalToneSchedule
    {
        public static ToneSegment[] Build(VocalsPart part)
        {
            if (part == null)
            {
                return Array.Empty<ToneSegment>();
            }

            var segments = new List<ToneSegment>();
            foreach (var phrase in part.NotePhrases)
            {
                foreach (var note in phrase.PhraseParentNote.ChildNotes)
                {
                    AppendNote(segments, note);
                }
            }

            return segments.ToArray();
        }

        private static void AppendNote(List<ToneSegment> segments, VocalNote note)
        {
            if (note.IsNonPitched || note.IsPercussion)
            {
                return;
            }

            var current = note;
            segments.Add(new ToneSegment(current.Time, current.TimeEnd, current.Pitch, current.Pitch));

            foreach (var child in note.ChildNotes)
            {
                if (child.IsNonPitched || child.IsPercussion)
                {
                    break;
                }

                // The gap between two pitches is the slide; skip it when they are contiguous.
                if (child.Time > current.TimeEnd)
                {
                    segments.Add(new ToneSegment(current.TimeEnd, child.Time, current.Pitch, child.Pitch));
                }

                segments.Add(new ToneSegment(child.Time, child.TimeEnd, child.Pitch, child.Pitch));
                current = child;
            }
        }
    }
}
