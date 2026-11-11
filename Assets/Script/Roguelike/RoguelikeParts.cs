using System.Collections.Generic;
using YARG.Core;

namespace YARG.Roguelike
{
    /// <summary>A part (instrument track) charted in a song, with its intensity (-1 if unknown).</summary>
    public readonly struct ChartedPart
    {
        public readonly Instrument Instrument;
        public readonly int        Intensity;

        public ChartedPart(Instrument instrument, int intensity)
        {
            Instrument = instrument;
            Intensity = intensity;
        }

        public override string ToString() => $"{Instrument} ({Intensity})";
    }

    /// <summary>
    /// Decides which parts of a song a roguelike player may play, and what intensity tier the song
    /// counts as for them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Mirrors normal play: if the song charts the player's preferred instrument, that is the part
    /// they play. If it doesn't, they may substitute any other part their controller can play (e.g.
    /// bass, rhythm or keys on a guitar controller), as Difficulty Select already allows.
    /// </para>
    /// <para>
    /// A song is rated by the <em>easiest</em> part the player is allowed to pick. That way a round
    /// never offers a song that can be played below the round's tier by picking an easy substitute;
    /// the worst case is that a player chooses a harder substitute than they needed to.
    /// </para>
    /// </remarks>
    public static class RoguelikeParts
    {
        /// <param name="preferred">The player's preferred instrument.</param>
        /// <param name="playableParts">
        /// Parts charted in the song that the player's controller can play (the song's charted parts
        /// filtered by the game mode's possible instruments for that song).
        /// </param>
        public static List<ChartedPart> GetAllowedParts(Instrument preferred, IEnumerable<ChartedPart> playableParts)
        {
            var allowed = new List<ChartedPart>();
            foreach (var part in playableParts)
            {
                if (part.Instrument == preferred)
                {
                    // The preferred part is charted, so it's the only one allowed
                    allowed.Clear();
                    allowed.Add(part);
                    return allowed;
                }

                allowed.Add(part);
            }
            return allowed;
        }

        /// <summary>
        /// The intensity tier the song counts as for this player, or -1 if it can't be offered (no
        /// playable part, or an allowed part has an unknown intensity).
        /// </summary>
        public static int GetRating(Instrument preferred, IEnumerable<ChartedPart> playableParts)
        {
            var allowed = GetAllowedParts(preferred, playableParts);
            if (allowed.Count == 0)
            {
                return -1;
            }

            int rating = int.MaxValue;
            foreach (var part in allowed)
            {
                if (part.Intensity < 0)
                {
                    // An unrated part could be anything, so the song can't be placed in a tier
                    return -1;
                }
                if (part.Intensity < rating)
                {
                    rating = part.Intensity;
                }
            }
            return rating;
        }
    }
}
