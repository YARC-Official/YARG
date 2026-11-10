using System;
using YARG.Core.Song;

namespace YARG.Roguelike
{
    /// <summary>
    /// Identifies the roguelike round an in-progress song belongs to. Carried from the roguelike menu
    /// through gameplay to the score-save path, like <see cref="YARG.Career.CareerContext"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="PersistentState"/> is reset when the player quits out of a song, which drops this
    /// context. The run itself is held by <see cref="RoguelikeSession"/>, so it survives that, and the
    /// menu can detect a quit by the run still having a selected song with no result.
    /// </remarks>
    public class RoguelikeContext
    {
        public Guid        RunId;
        public int         Round;
        public int         Attempt;
        public HashWrapper SongHash;
    }

    /// <summary>Holds the active run for the lifetime of the game session.</summary>
    public static class RoguelikeSession
    {
        public static RoguelikeRun Current { get; private set; }

        public static RoguelikeRun Start(RoguelikeRun run)
        {
            Current = run ?? throw new ArgumentNullException(nameof(run));
            return Current;
        }

        public static void Clear()
        {
            Current = null;
        }

        /// <summary>True if <paramref name="context"/> refers to the active run's current round.</summary>
        public static bool Matches(RoguelikeContext context)
        {
            var run = Current;
            return context != null && run != null && !run.IsOver &&
                context.RunId == run.Id &&
                context.Round == run.Round &&
                context.Attempt == run.Attempt &&
                run.SelectedSong is { } selected && selected.Equals(context.SongHash);
        }
    }
}
