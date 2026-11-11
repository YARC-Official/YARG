using System.Collections.Generic;
using System.Linq;
using YARG.Core;
using YARG.Core.Song;
using YARG.Song;

namespace YARG.Roguelike
{
    /// <summary>
    /// Connects <see cref="RoguelikeRun"/> to the real song library. Everything that touches
    /// <see cref="SongEntry"/> or <see cref="SongContainer"/> lives here, so the run and picker logic
    /// stay testable outside Unity.
    /// </summary>
    public static class RoguelikeLibrary
    {
        /// <summary>The parts of a song the run's controller can play, with their intensities.</summary>
        public static List<ChartedPart> GetPlayableParts(SongEntry song, GameMode gameMode)
        {
            var parts = new List<ChartedPart>();
            foreach (var instrument in gameMode.PossibleInstrumentsForSong(song))
            {
                if (song.HasInstrument(instrument))
                {
                    parts.Add(new ChartedPart(instrument, song[instrument].Intensity));
                }
            }
            return parts;
        }

        /// <summary>The intensity tier a song counts as in this run, or -1 if it can't be offered.</summary>
        public static int GetRating(RoguelikeRun run, SongEntry song)
        {
            return RoguelikeParts.GetRating(run.PreferredInstrument, GetPlayableParts(song, run.GameMode));
        }

        /// <summary>Parts the player may choose at Difficulty Select for this song.</summary>
        public static List<Instrument> GetAllowedInstruments(RoguelikeRun run, SongEntry song)
        {
            return RoguelikeParts
                .GetAllowedParts(run.PreferredInstrument, GetPlayableParts(song, run.GameMode))
                .Select(part => part.Instrument)
                .ToList();
        }

        /// <summary>
        /// Roll the current round's choices from the library if it doesn't have any yet. Choices are
        /// stored on the run, so calling this again for the same round is a no-op.
        /// </summary>
        public static void EnsureChoices(RoguelikeRun run)
        {
            if (!run.NeedsChoices)
            {
                return;
            }

            var played = run.PlayedSongs;
            var picks = RoguelikeSongPicker.Pick(
                SongContainer.Songs,
                song => GetRating(run, song),
                song => song.IsDuplicate || played.Contains(song.Hash),
                run.TargetIntensity,
                run.Rules.ChoicesPerRound,
                run.Rules.MinIntensity,
                run.Rules.MaxIntensity,
                run.CreateRoundRandom());

            run.SetChoices(picks.Select(song => song.Hash));
        }

        /// <summary>Find a song in the library by hash, or null if it's no longer there.</summary>
        public static SongEntry Resolve(HashWrapper hash)
        {
            return SongContainer.SongsByHash.TryGetValue(hash, out var songs) && songs.Count > 0
                ? songs[0]
                : null;
        }

        /// <summary>
        /// How many offerable songs each tier has for this controller and preferred part. Useful for
        /// warning the player before a run starts if their library is too thin for some rounds.
        /// </summary>
        public static Dictionary<int, int> CountSongsPerTier(GameMode gameMode, Instrument preferred)
        {
            var counts = new Dictionary<int, int>();
            foreach (var song in SongContainer.Songs)
            {
                if (song.IsDuplicate)
                {
                    continue;
                }

                int rating = RoguelikeParts.GetRating(preferred, GetPlayableParts(song, gameMode));
                if (rating >= 0)
                {
                    counts.TryGetValue(rating, out int count);
                    counts[rating] = count + 1;
                }
            }
            return counts;
        }
    }
}
