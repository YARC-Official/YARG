using System;
using System.Runtime.CompilerServices;

namespace YARG.Core
{
    // !DO NOT MODIFY THE VALUES OR ORDER OF THESE ENUMS!
    // Since they are serialized in replays, they *must* remain the same across changes.
    // Add new values in the gaps between reserved ranges, or reserve a new range at the end of the enum.

    /// <summary>
    /// Available game modes.
    /// </summary>
    public enum GameMode : byte
    {
        // Game modes are reserved in multiples of 5
        // 0-4: Guitar
        FiveFretGuitar = 0,
        SixFretGuitar = 1,

        // 5-9: Drums
        FourLaneDrums = 5,
        FiveLaneDrums = 6,
        // EliteDrums = 7,

        // 10-14: Pro instruments
        ProGuitar = 10,
        ProKeys = 11,

        // 15-19: Vocals
        Vocals = 15,

        // 20-24: Other
        // Dj = 20,
    }

    /// <summary>
    /// Available instruments.
    /// </summary>
    public enum Instrument : byte
    {
        // Instruments are reserved in multiples of 10
        // 0-9: 5-fret guitar
        FiveFretGuitar = 0,
        FiveFretBass = 1,
        FiveFretRhythm = 2,
        FiveFretCoopGuitar = 3,
        Keys = 4,

        // 10-19: 6-fret guitar
        SixFretGuitar = 10,
        SixFretBass = 11,
        SixFretRhythm = 12,
        SixFretCoopGuitar = 13,

        // 20-29: Drums
        FourLaneDrums = 20,
        ProDrums = 21,
        FiveLaneDrums = 22,
        EliteDrums = 23,

        // 30-39: Pro instruments
        ProGuitar_17Fret = 30,
        ProGuitar_22Fret = 31,
        ProBass_17Fret = 32,
        ProBass_22Fret = 33,

        ProKeys = 34,

        // 40-49: Vocals
        Vocals = 40,
        Harmony = 41,

        // 50-59: DJ
        // DjSingle = 50,
        // DjDouble = 51,

        Band = byte.MaxValue
    }

    /// <summary>
    /// Available difficulty levels.
    /// </summary>
    public enum Difficulty : byte
    {
        Beginner = 0,
        Easy = 1,
        Medium = 2,
        Hard = 3,
        Expert = 4,
        ExpertPlus = 5,
    }

    /// <summary>
    /// Available difficulty levels.
    /// <remarks>DO NOT MAKE THIS LARGER THAN A BYTE!</remarks>
    /// </summary>
    [Flags]
    public enum DifficultyMask : byte
    {
        None = 0,

        Beginner   = 1 << Difficulty.Beginner,
        Easy       = 1 << Difficulty.Easy,
        Medium     = 1 << Difficulty.Medium,
        Hard       = 1 << Difficulty.Hard,
        Expert     = 1 << Difficulty.Expert,
        ExpertPlus = 1 << Difficulty.ExpertPlus,

        All = Beginner | Easy | Medium | Hard | Expert | ExpertPlus,
    }

    public static class ChartEnumExtensions
    {
        public static GameMode ToGameMode(this Instrument instrument)
        {
            return instrument switch
            {
                Instrument.FiveFretGuitar or
                Instrument.FiveFretBass or
                Instrument.FiveFretRhythm or
                Instrument.FiveFretCoopGuitar or
                Instrument.Keys => GameMode.FiveFretGuitar,

                Instrument.SixFretGuitar or
                Instrument.SixFretBass or
                Instrument.SixFretRhythm or
                Instrument.SixFretCoopGuitar => GameMode.SixFretGuitar,

                Instrument.FourLaneDrums or
                Instrument.ProDrums => GameMode.FourLaneDrums,

                Instrument.FiveLaneDrums => GameMode.FiveLaneDrums,

                // Instrument.EliteDrums => GameMode.EliteDrums,

                Instrument.ProGuitar_17Fret or
                Instrument.ProGuitar_22Fret or
                Instrument.ProBass_17Fret or
                Instrument.ProBass_22Fret => GameMode.ProGuitar,

                Instrument.ProKeys => GameMode.ProKeys,

                Instrument.Vocals or
                Instrument.Harmony => GameMode.Vocals,

                // Instrument.DjSingle => GameMode.Dj,
                // Instrument.DjDouble => GameMode.Dj,

                _ => throw new NotImplementedException($"Unhandled instrument {instrument}!")
            };
        }

        public static Instrument[] PossibleInstruments(this GameMode gameMode)
        {
            return gameMode switch
            {
                GameMode.FiveFretGuitar => new[]
                {
                    Instrument.FiveFretGuitar,
                    Instrument.FiveFretBass,
                    Instrument.FiveFretRhythm,
                    Instrument.FiveFretCoopGuitar,
                    Instrument.Keys,
                },
                GameMode.SixFretGuitar  => new[]
                {
                    Instrument.SixFretGuitar,
                    Instrument.SixFretBass,
                    Instrument.SixFretRhythm,
                    Instrument.SixFretCoopGuitar,
                },
                GameMode.FourLaneDrums  => new[]
                {
                    Instrument.FourLaneDrums,
                    Instrument.ProDrums,
                },
                GameMode.FiveLaneDrums  => new[]
                {
                    Instrument.FiveLaneDrums
                },
                //GameMode.EliteDrums     => new[]
                //{
                //     Instrument.EliteDrums,
                //},
                GameMode.ProGuitar      => new[]
                {
                    Instrument.ProGuitar_17Fret,
                    Instrument.ProGuitar_22Fret,
                    Instrument.ProBass_17Fret,
                    Instrument.ProBass_22Fret,
                },
                GameMode.ProKeys        => new[]
                {
                    Instrument.ProKeys
                },
                GameMode.Vocals         => new[]
                {
                    Instrument.Vocals,
                    Instrument.Harmony
                },
                // GameMode.Dj             => new[]
                // {
                //     Instrument.DjSingle,
                //     Instrument.DjDouble,
                // },
                _  => throw new NotImplementedException($"Unhandled game mode {gameMode}!")
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DifficultyMask ToDifficultyMask(this Difficulty difficulty)
        {
            return (DifficultyMask) (1 << (int) difficulty);
        }

        public static Difficulty ToDifficulty(this DifficultyMask difficulty)
        {
            return difficulty switch
            {
                DifficultyMask.Beginner   => Difficulty.Beginner,
                DifficultyMask.Easy       => Difficulty.Easy,
                DifficultyMask.Medium     => Difficulty.Medium,
                DifficultyMask.Hard       => Difficulty.Hard,
                DifficultyMask.Expert     => Difficulty.Expert,
                DifficultyMask.ExpertPlus => Difficulty.ExpertPlus,
                _ => throw new ArgumentException($"Cannot convert difficulty mask {difficulty} into a single difficulty!")
            };
        }
    }
}