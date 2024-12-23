// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace MoonscraperChartEditor.Song.IO
{
    internal class NoteFlagPriority
    {
        // Flags to skip adding if the corresponding flag is already present
        private static readonly Dictionary<MoonNote.Flags, MoonNote.Flags> NoteBlockingFlagsLookup = new()
        {
            { MoonNote.Flags.Forced, MoonNote.Flags.Tap },
            { MoonNote.Flags.ProDrums_Ghost, MoonNote.Flags.ProDrums_Accent },
        };

        // Flags to remove if the corresponding flag is being added
        private static readonly Dictionary<MoonNote.Flags, MoonNote.Flags> NoteFlagsToRemoveLookup =
            NoteBlockingFlagsLookup.ToDictionary((i) => i.Value, (i) => i.Key);

        public static readonly NoteFlagPriority Forced = new(MoonNote.Flags.Forced);
        public static readonly NoteFlagPriority Tap = new(MoonNote.Flags.Tap);
        public static readonly NoteFlagPriority InstrumentPlus = new(MoonNote.Flags.InstrumentPlus);
        public static readonly NoteFlagPriority Cymbal = new(MoonNote.Flags.ProDrums_Cymbal);
        public static readonly NoteFlagPriority Accent = new(MoonNote.Flags.ProDrums_Accent);
        public static readonly NoteFlagPriority Ghost = new(MoonNote.Flags.ProDrums_Ghost);

        private static readonly List<NoteFlagPriority> priorities = new()
        {
            Forced,
            Tap,
            InstrumentPlus,
            Cymbal,
            Accent,
            Ghost,
        };

        public MoonNote.Flags flagToAdd { get; } = MoonNote.Flags.None;
        public MoonNote.Flags blockingFlag { get; } = MoonNote.Flags.None;
        public MoonNote.Flags flagToRemove { get; } = MoonNote.Flags.None;

        public NoteFlagPriority(MoonNote.Flags flag)
        {
            flagToAdd = flag;

            if (NoteBlockingFlagsLookup.TryGetValue(flagToAdd, out var blockingFlag))
            {
                this.blockingFlag = blockingFlag;
            }

            if (NoteFlagsToRemoveLookup.TryGetValue(flagToAdd, out var flagToRemove))
            {
                this.flagToRemove = flagToRemove;
            }
        }

        public bool TryApplyToNote(MoonNote note)
        {
            // Don't add if the flag to be added is lower-priority than a conflicting, already-added flag
            if (blockingFlag != MoonNote.Flags.None && note.flags.HasFlag(blockingFlag))
            {
                return false;
            }

            // Flag can be added without issue
            note.flags |= flagToAdd;

            // Remove flags that are lower-priority than the added flag
            if (flagToRemove != MoonNote.Flags.None && note.flags.HasFlag(flagToRemove))
            {
                note.flags &= ~flagToRemove;
            }

            return true;
        }

        public bool AreFlagsValid(MoonNote.Flags flags)
        {
            if (flagToAdd == MoonNote.Flags.None)
            {
                // No flag to validate against
                return true;
            }

            if (blockingFlag != MoonNote.Flags.None)
            {
                if (flags.HasFlag(blockingFlag) && flags.HasFlag(flagToAdd))
                {
                    // Note has conflicting flags
                    return false;
                }
            }

            if (flagToRemove != MoonNote.Flags.None)
            {
                if (flags.HasFlag(flagToAdd) && flags.HasFlag(flagToRemove))
                {
                    // Note has conflicting flags
                    return false;
                }
            }

            return true;
        }

        public static bool AreFlagsValidForAll(MoonNote.Flags flags, [NotNullWhen(false)] out NoteFlagPriority? invalidPriority)
        {
            foreach (var priority in priorities)
            {
                if (!priority.AreFlagsValid(flags))
                {
                    invalidPriority = priority;
                    return false;
                }
            }

            invalidPriority = null;
            return true;
        }
    }
}