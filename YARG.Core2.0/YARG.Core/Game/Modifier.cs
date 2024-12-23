using System;
using System.Collections.Generic;

namespace YARG.Core.Game
{
    [Flags]
    public enum Modifier : ulong
    {
        None          = 0,
        AllStrums     = 1 << 0,
        AllHopos      = 1 << 1,
        AllTaps       = 1 << 2,
        HoposToTaps   = 1 << 3,
        TapsToHopos   = 1 << 4,
        NoteShuffle   = 1 << 5,
        NoKicks       = 1 << 6,
        UnpitchedOnly = 1 << 7,
        NoDynamics    = 1 << 8,
    }

    public static class ModifierConflicts
    {
        // We can essentially treat a set of conflicting modifiers as a group, since they
        // conflict in both ways (i.e. all strums conflicts with all HOPOs, and vice versa).
        // Returning a list of the conflicting modifiers, and simply removing them, takes
        // care of all of the possibilities. A modifier can be a part of multiple groups,
        // which is why we use a list here.
        private static readonly List<Modifier> _conflictingModifiers = new()
        {
            Modifier.AllStrums   |
            Modifier.AllHopos    |
            Modifier.AllTaps     |
            Modifier.HoposToTaps |
            Modifier.TapsToHopos,
        };

        public static Modifier PossibleModifiers(this GameMode gameMode)
        {
            return gameMode switch
            {
                GameMode.FiveFretGuitar =>
                    Modifier.AllStrums   |
                    Modifier.AllHopos    |
                    Modifier.AllTaps     |
                    Modifier.HoposToTaps |
                    Modifier.TapsToHopos,

                GameMode.FourLaneDrums or
                GameMode.FiveLaneDrums =>
                    Modifier.NoKicks    |
                    Modifier.NoDynamics,

                GameMode.Vocals =>
                    Modifier.UnpitchedOnly,

                GameMode.SixFretGuitar or
            //  GameMode.EliteDrums    or
                GameMode.ProGuitar     or
            //  GameMode.Dj            or
                GameMode.ProKeys       => Modifier.None,

                _  => throw new NotImplementedException($"Unhandled game mode {gameMode}!")
            };
        }

        public static Modifier FromSingleModifier(Modifier modifier)
        {
            var output = Modifier.None;

            foreach (var conflictSet in _conflictingModifiers)
            {
                if ((conflictSet & modifier) == 0) continue;

                // Set conflicts
                output |= conflictSet;

                // Make sure to get rid of the modifier itself
                output &= ~modifier;
            }

            return output;
        }
    }
}