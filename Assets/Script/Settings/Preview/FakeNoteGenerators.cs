using System.Collections.Generic;
using YARG.Core.Chart;
using YARG.Core.Engine.Keys;
using YARG.Themes;

// pattern: Functional Core

namespace YARG.Settings.Preview
{
    public interface IFakeNoteRandom
    {
        int Range(int minInclusive, int maxExclusive);
    }

    public readonly struct FakeNoteSpotlight
    {
        public readonly int Fret;
        public readonly bool CenterNote;
        public readonly bool Cymbal;
        public readonly bool LeftyFlip;

        public FakeNoteSpotlight(int fret, bool centerNote, bool cymbal, bool leftyFlip)
        {
            Fret = fret;
            CenterNote = centerNote;
            Cymbal = cymbal;
            LeftyFlip = leftyFlip;
        }
    }

    public interface IFakeNoteGenerator
    {
        FakeNoteData CreateNote(double time, IFakeNoteRandom random);

        FakeNoteData CreateSpotlightNote(double time, FakeNoteSpotlight spotlight,
            IFakeNoteRandom random);

        FakeNoteData CreateTypeSpotlightNote(double time, ThemeNoteType noteType,
            IFakeNoteRandom random);

        IEnumerable<FakeNoteData> CreateChordNotes(double time, FakeNoteData baseNote,
            IFakeNoteRandom random);
    }

    public sealed class FiveFretGuitarFakeNoteGenerator : IFakeNoteGenerator
    {
        public FakeNoteData CreateNote(double time, IFakeNoteRandom random)
        {
            int fret = random.Range(0, 6);
            if (fret == 0)
            {
                return CreateOpenNote(time, random.Range(0, 2) == 0 ? ThemeNoteType.Open : ThemeNoteType.OpenHOPO);
            }

            return CreateFretNote(time, fret, CreateGuitarNoteType(random));
        }

        public FakeNoteData CreateSpotlightNote(double time, FakeNoteSpotlight spotlight,
            IFakeNoteRandom random)
        {
            if (spotlight.CenterNote)
            {
                return new FakeNoteData
                {
                    Time = time,
                    Fret = (int) FiveFretGuitarFret.Open,
                    CenterNote = true,
                    NoteType = ThemeNoteType.Open
                };
            }

            int fret = spotlight.Fret;
            if (spotlight.LeftyFlip)
            {
                fret = 6 - fret;
            }

            return new FakeNoteData
            {
                Time = time,
                Fret = fret,
                CenterNote = false,
                NoteType = CreateGuitarNoteType(random)
            };
        }

        public FakeNoteData CreateTypeSpotlightNote(double time, ThemeNoteType noteType,
            IFakeNoteRandom random)
        {
            if (noteType is ThemeNoteType.Open or ThemeNoteType.OpenHOPO)
            {
                return CreateOpenNote(time, noteType);
            }

            return CreateFretNote(time, random.Range(1, 6), noteType);
        }

        public IEnumerable<FakeNoteData> CreateChordNotes(double time, FakeNoteData baseNote,
            IFakeNoteRandom random)
        {
            if (baseNote.CenterNote)
            {
                yield break;
            }

            int placed = 0;
            int denominator = 4;
            for (int fret = baseNote.Fret + 1; fret <= 5 && placed < 2; fret++)
            {
                // A three-note chord may not contain both green and orange.
                if (placed == 1 && fret == 5 && baseNote.Fret == 1)
                {
                    continue;
                }

                if (random.Range(0, denominator) != 0)
                {
                    continue;
                }

                placed++;
                denominator *= 2;
                yield return CopyOverrides(baseNote, new FakeNoteData
                {
                    Time = time,
                    Fret = fret,
                    CenterNote = false,
                    NoteType = baseNote.NoteType
                });
            }
        }

        private static ThemeNoteType CreateGuitarNoteType(IFakeNoteRandom random) => random.Range(0, 3) switch
        {
            0 => ThemeNoteType.Normal,
            1 => ThemeNoteType.HOPO,
            _ => ThemeNoteType.Tap
        };

        private static FakeNoteData CreateFretNote(double time, int fret, ThemeNoteType noteType) => new()
        {
            Time = time,
            Fret = fret,
            CenterNote = false,
            NoteType = noteType
        };

        private static FakeNoteData CreateOpenNote(double time, ThemeNoteType noteType) => new()
        {
            Time = time,
            Fret = (int) FiveFretGuitarFret.Open,
            CenterNote = true,
            NoteType = noteType
        };

        private static FakeNoteData CopyOverrides(FakeNoteData source, FakeNoteData note)
        {
            note.ForceMiss = source.ForceMiss;
            note.ForceStarPower = source.ForceStarPower;
            return note;
        }
    }

    public sealed class DrumFakeNoteGenerator : IFakeNoteGenerator
    {
        private readonly bool _fiveLane;

        public DrumFakeNoteGenerator(bool fiveLane)
        {
            _fiveLane = fiveLane;
        }

        public FakeNoteData CreateNote(double time, IFakeNoteRandom random)
        {
            int fret = random.Range(0, _fiveLane ? 6 : 5);
            return CreateNoteForFret(time, fret, random);
        }

        public FakeNoteData CreateSpotlightNote(double time, FakeNoteSpotlight spotlight,
            IFakeNoteRandom random)
        {
            if (spotlight.CenterNote)
            {
                return new FakeNoteData
                {
                    Time = time,
                    Fret = 0,
                    CenterNote = true,
                    NoteType = ThemeNoteType.Kick
                };
            }

            return new FakeNoteData
            {
                Time = time,
                Fret = spotlight.Fret,
                CenterNote = false,
                NoteType = CreateDrumNoteType(spotlight.Cymbal, random)
            };
        }

        public FakeNoteData CreateTypeSpotlightNote(double time, ThemeNoteType noteType,
            IFakeNoteRandom random)
        {
            int fret = random.Range(1, _fiveLane ? 6 : 5);
            var note = CreateNoteForFret(time, fret, random);
            note.NoteType = noteType switch
            {
                ThemeNoteType.Ghost when note.NoteType is ThemeNoteType.Cymbal
                    or ThemeNoteType.CymbalAccent or ThemeNoteType.CymbalGhost => ThemeNoteType.CymbalGhost,
                ThemeNoteType.Accent when note.NoteType is ThemeNoteType.Cymbal
                    or ThemeNoteType.CymbalAccent or ThemeNoteType.CymbalGhost => ThemeNoteType.CymbalAccent,
                _ => noteType
            };
            return note;
        }

        public IEnumerable<FakeNoteData> CreateChordNotes(double time, FakeNoteData baseNote,
            IFakeNoteRandom random)
        {
            if (baseNote.CenterNote)
            {
                // Sometimes add a pad/cymbal companion (same probability as
                // non-kick extras) so kick chord generation is independent.
                if (random.Range(0, 3) == 0)
                {
                    int fret = random.Range(1, _fiveLane ? 6 : 5);
                    yield return CopyOverrides(baseNote, CreateNoteForFret(time, fret, random));
                }
                yield break;
            }

            if (random.Range(0, 3) == 0)
            {
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    var extra = CreateNote(time, random);
                    if (!extra.CenterNote && extra.Fret != baseNote.Fret)
                    {
                        yield return CopyOverrides(baseNote, extra);
                        break;
                    }
                }
            }

            if (random.Range(0, 3) == 0)
            {
                yield return CopyOverrides(baseNote, new FakeNoteData
                {
                    Time = time,
                    Fret = 0,
                    CenterNote = true,
                    NoteType = ThemeNoteType.Kick
                });
            }
        }

        private FakeNoteData CreateNoteForFret(double time, int fret, IFakeNoteRandom random)
        {
            if (fret == 0)
            {
                return new FakeNoteData
                {
                    Time = time,
                    Fret = 0,
                    CenterNote = true,
                    NoteType = ThemeNoteType.Kick
                };
            }

            bool cymbal = _fiveLane ? fret is 2 or 4 : fret != 1 && random.Range(0, 100) < 75;
            return new FakeNoteData
            {
                Time = time,
                Fret = fret,
                CenterNote = false,
                NoteType = CreateDrumNoteType(cymbal, random)
            };
        }

        private static ThemeNoteType CreateDrumNoteType(bool cymbal, IFakeNoteRandom random)
        {
            int variant = random.Range(0, 100);
            if (cymbal)
            {
                return variant < 75 ? ThemeNoteType.Cymbal
                    : variant < 90 ? ThemeNoteType.CymbalAccent
                    : ThemeNoteType.CymbalGhost;
            }

            return variant < 75 ? ThemeNoteType.Normal
                : variant < 90 ? ThemeNoteType.Accent
                : ThemeNoteType.Ghost;
        }

        private static FakeNoteData CopyOverrides(FakeNoteData source, FakeNoteData note)
        {
            note.ForceMiss = source.ForceMiss;
            note.ForceStarPower = source.ForceStarPower;
            return note;
        }
    }

    public sealed class FiveLaneKeysFakeNoteGenerator : IFakeNoteGenerator
    {
        public FakeNoteData CreateNote(double time, IFakeNoteRandom random) => new()
        {
            Time = time,
            Fret = random.Range(1, 6),
            CenterNote = false,
            NoteType = ThemeNoteType.Normal
        };

        public FakeNoteData CreateSpotlightNote(double time, FakeNoteSpotlight spotlight,
            IFakeNoteRandom random)
        {
            if (spotlight.CenterNote)
            {
                return new FakeNoteData
                {
                    Time = time,
                    Fret = (int) FiveFretGuitarFret.Open,
                    CenterNote = true,
                    NoteType = ThemeNoteType.Open
                };
            }

            return new FakeNoteData
            {
                Time = time,
                Fret = spotlight.Fret,
                CenterNote = false,
                NoteType = ThemeNoteType.Normal
            };
        }

        public FakeNoteData CreateTypeSpotlightNote(double time, ThemeNoteType noteType,
            IFakeNoteRandom random)
        {
            if (noteType is ThemeNoteType.Open or ThemeNoteType.OpenHOPO)
            {
                return new FakeNoteData
                {
                    Time = time,
                    Fret = (int) FiveFretGuitarFret.Open,
                    CenterNote = true,
                    NoteType = noteType
                };
            }

            return CreateNote(time, random);
        }

        public IEnumerable<FakeNoteData> CreateChordNotes(double time, FakeNoteData baseNote,
            IFakeNoteRandom random)
        {
            if (baseNote.CenterNote)
            {
                yield break;
            }

            int placed = 0;
            int denominator = 4;
            for (int fret = baseNote.Fret + 1; fret <= 5 && placed < 2; fret++)
            {
                if (random.Range(0, denominator) != 0)
                {
                    continue;
                }

                placed++;
                denominator *= 2;
                yield return CopyOverrides(baseNote, new FakeNoteData
                {
                    Time = time,
                    Fret = fret,
                    CenterNote = false,
                    NoteType = ThemeNoteType.Normal
                });
            }
        }

        private static FakeNoteData CopyOverrides(FakeNoteData source, FakeNoteData note)
        {
            note.ForceMiss = source.ForceMiss;
            note.ForceStarPower = source.ForceStarPower;
            return note;
        }
    }

    public sealed class SixFretGuitarFakeNoteGenerator : IFakeNoteGenerator
    {
        public FakeNoteData CreateNote(double time, IFakeNoteRandom random)
        {
            // 0 = Open, 1-6 = 6 frets, 7 = Wildcard
            int fret = random.Range(0, 8);

            // Open notes
            if (fret == 0)
            {
                return new FakeNoteData
                {
                    Time = time,
                    Fret = (int) SixFretGuitarFret.Open,
                    CenterNote = true,
                    NoteType = ThemeNoteType.Open
                };
            }

            // Wildcard
            if (fret == 7)
            {
                return new FakeNoteData
                {
                    Time = time,
                    Fret = (int) SixFretGuitarFret.Wildcard,
                    CenterNote = true,
                    NoteType = ThemeNoteType.Wildcard
                };
            }

            return new FakeNoteData
            {
                Time = time,
                Fret = fret,
                CenterNote = false,
                NoteType = CreateFretNoteType(fret, random)
            };
        }

        public FakeNoteData CreateSpotlightNote(double time, FakeNoteSpotlight spotlight,
            IFakeNoteRandom random)
        {
            if (spotlight.CenterNote)
            {
                return new FakeNoteData
                {
                    Time = time,
                    Fret = (int) SixFretGuitarFret.Open,
                    CenterNote = true,
                    NoteType = ThemeNoteType.Open
                };
            }

            return new FakeNoteData
            {
                Time = time,
                Fret = spotlight.Fret,
                CenterNote = false,
                NoteType = CreateFretNoteType(spotlight.Fret, random)
            };
        }

        public FakeNoteData CreateTypeSpotlightNote(double time, ThemeNoteType noteType,
            IFakeNoteRandom random)
        {
            switch (noteType)
            {
                case ThemeNoteType.Open:
                    return new FakeNoteData
                    {
                        Time = time,
                        Fret = (int) SixFretGuitarFret.Open,
                        CenterNote = true,
                        NoteType = noteType
                    };
                case ThemeNoteType.Wildcard:
                    return new FakeNoteData
                    {
                        Time = time,
                        Fret = (int) SixFretGuitarFret.Wildcard,
                        CenterNote = true,
                        NoteType = noteType
                    };
                case ThemeNoteType.SixFretUp or ThemeNoteType.SixFretUpHOPO
                    or ThemeNoteType.SixFretUpTap:
                    // Black frets (1-3) render as "up" notes
                    return new FakeNoteData
                    {
                        Time = time,
                        Fret = random.Range(1, 4),
                        CenterNote = false,
                        NoteType = noteType
                    };
                case ThemeNoteType.SixFretDown or ThemeNoteType.SixFretDownHOPO
                    or ThemeNoteType.SixFretDownTap:
                    // White frets (4-6) render as "down" notes
                    return new FakeNoteData
                    {
                        Time = time,
                        Fret = random.Range(4, 7),
                        CenterNote = false,
                        NoteType = noteType
                    };
                case ThemeNoteType.SixFretBarre or ThemeNoteType.SixFretBarreHOPO or ThemeNoteType.SixFretBarreTap:
                    return new FakeNoteData
                    {
                        Time = time,
                        Fret = random.Range(1, 7),
                        CenterNote = false,
                        NoteType = noteType
                    };
                default:
                    var note = CreateNote(time, random);
                    note.NoteType = noteType;
                    return note;
            }
        }

        public IEnumerable<FakeNoteData> CreateChordNotes(double time, FakeNoteData baseNote,
            IFakeNoteRandom random)
        {
            // Six-fret preview has no chord generation
            yield break;
        }

        /// <summary>
        /// Picks the note type for a fretted six-fret note: black frets (1-3) are
        /// "up" notes, white frets (4-6) are "down" notes, with a 30% chance of a
        /// barre. Up/down/barre notes randomly use the strum, HOPO, or Tap variant.
        /// </summary>
        private static ThemeNoteType CreateFretNoteType(int fret, IFakeNoteRandom random)
        {
            bool isUp = fret is >= 1 and <= 3; // Black1(1)-Black3(3)
            bool isBarre = random.Range(0, 100) < 30;

            // Map to theme note type
            ThemeNoteType themeType = isBarre ? ThemeNoteType.SixFretBarre
                : isUp ? ThemeNoteType.SixFretUp : ThemeNoteType.SixFretDown;

            // Override to HOPO/Tap variants randomly
            return random.Range(0, 3) switch
            {
                0 => themeType, // Strum
                1 => (isUp ? ThemeNoteType.SixFretUpHOPO :
                      isBarre ? ThemeNoteType.SixFretBarreHOPO :
                               ThemeNoteType.SixFretDownHOPO),
                _ => (isUp ? ThemeNoteType.SixFretUpTap :
                      isBarre ? ThemeNoteType.SixFretBarreTap :
                               ThemeNoteType.SixFretDownTap)
            };
        }
    }

    public sealed class ProKeysFakeNoteGenerator : IFakeNoteGenerator
    {
        public FakeNoteData CreateNote(double time, IFakeNoteRandom random)
        {
            int fret = random.Range(0, 17);
            return new FakeNoteData
            {
                Time = time,
                Fret = fret,
                CenterNote = false,
                NoteType = GetNoteType(fret)
            };
        }

        public FakeNoteData CreateSpotlightNote(double time, FakeNoteSpotlight spotlight,
            IFakeNoteRandom random)
        {
            return new FakeNoteData
            {
                Time = time,
                Fret = spotlight.Fret,
                CenterNote = false,
                NoteType = GetNoteType(spotlight.Fret)
            };
        }

        public FakeNoteData CreateTypeSpotlightNote(double time, ThemeNoteType noteType,
            IFakeNoteRandom random)
        {
            if (noteType is ThemeNoteType.White or ThemeNoteType.Black)
            {
                bool black = noteType == ThemeNoteType.Black;
                int colorIndex = random.Range(0,
                    black ? ProKeysUtilities.BLACK_KEY_COUNT : ProKeysUtilities.WHITE_KEY_COUNT);
                return new FakeNoteData
                {
                    Time = time,
                    Fret = ProKeysUtilities.GetKeyIndexForColor(black, colorIndex),
                    CenterNote = false,
                    NoteType = noteType
                };
            }

            var note = CreateNote(time, random);
            note.NoteType = noteType;
            return note;
        }

        public IEnumerable<FakeNoteData> CreateChordNotes(double time, FakeNoteData baseNote,
            IFakeNoteRandom random)
        {
            int noteCount = 1;
            int denominator = 4;
            int fret = baseNote.Fret + 2;
            int maxFret = baseNote.Fret + 8;
            if (maxFret > 16)
            {
                maxFret = 16;
            }

            while (noteCount < 4 && fret <= maxFret)
            {
                if (random.Range(0, denominator) == 0)
                {
                    yield return CopyOverrides(baseNote, new FakeNoteData
                    {
                        Time = time,
                        Fret = fret,
                        CenterNote = false,
                        NoteType = GetNoteType(fret)
                    });
                    noteCount++;
                    denominator *= 2;
                    fret += 2;
                }
                else
                {
                    fret++;
                }
            }
        }

        private static ThemeNoteType GetNoteType(int fret) =>
            ProKeysUtilities.IsBlackKey(fret % 12) ? ThemeNoteType.Black : ThemeNoteType.White;

        private static FakeNoteData CopyOverrides(FakeNoteData source, FakeNoteData note)
        {
            note.ForceMiss = source.ForceMiss;
            note.ForceStarPower = source.ForceStarPower;
            return note;
        }
    }
}
