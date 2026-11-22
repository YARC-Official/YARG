using System;
using YARG.Core;
using YARG.Core.Logging;
using YARG.Core.Song;

namespace YARG.Roguelike
{
    /// <summary>
    /// Identifies the roguelike round an in-progress song belongs to. Carried from the roguelike menu
    /// through gameplay to the end of the song, like <see cref="YARG.Career.CareerContext"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="PersistentState"/> is reset when the player leaves the score screen or quits out of a
    /// song, which drops this context. The run itself is held by <see cref="RoguelikeSession"/>, so it
    /// survives that.
    /// </remarks>
    public class RoguelikeContext
    {
        public Guid        RunId;
        public int         Round;
        public int         Attempt;
        public HashWrapper SongHash;
    }

    /// <summary>
    /// Holds the active run for the lifetime of the game session, and is the single entry point that
    /// gameplay and menu code use to start songs and report results.
    /// </summary>
    public static class RoguelikeSession
    {
        public static RoguelikeRun Current { get; private set; }

        /// <summary>The profile playing the run. Other players sit out roguelike songs.</summary>
        public static Guid ProfileId { get; private set; }

        /// <summary>The outcome of the most recently recorded round, for the menu to show.</summary>
        public static RoguelikeRoundOutcome? LastOutcome { get; private set; }

        /// <summary>
        /// True while the current song (gameplay or its score screen) was started from a roguelike run.
        /// Used to hide restarts and other options that would let a round be retried for free.
        /// </summary>
        public static bool IsRoguelikeSong => GlobalVariables.State.CurrentRoguelike != null;

        public static RoguelikeRun Start(RoguelikeRun run, Guid profileId)
        {
            Current = run ?? throw new ArgumentNullException(nameof(run));
            ProfileId = profileId;
            LastOutcome = null;
            return Current;
        }

        public static void Clear()
        {
            Current = null;
            ProfileId = Guid.Empty;
            LastOutcome = null;
        }

        /// <summary>
        /// Select one of the current round's choices and set up the game state to play it. The caller
        /// then opens Difficulty Select as usual.
        /// </summary>
        public static void BeginSong(SongEntry song)
        {
            var run = Current ?? throw new InvalidOperationException("No roguelike run is active.");
            run.SelectSong(song.Hash);

            GlobalVariables.State.CurrentSong = song;
            GlobalVariables.State.IsPractice = false;
            GlobalVariables.State.CurrentCareer = null;
            GlobalVariables.State.CurrentRoguelike = new RoguelikeContext
            {
                RunId = run.Id,
                Round = run.Round,
                Attempt = run.Attempt,
                SongHash = song.Hash,
            };
            LastOutcome = null;
        }

        /// <summary>True if <paramref name="context"/> refers to the active run's current, unplayed round.</summary>
        public static bool Matches(RoguelikeContext context)
        {
            var run = Current;
            return context != null && run != null && !run.IsOver &&
                context.RunId == run.Id &&
                context.Round == run.Round &&
                context.Attempt == run.Attempt &&
                run.SelectedSong is { } selected && selected.Equals(context.SongHash);
        }

        /// <summary>Record a song that was played to the end.</summary>
        /// <param name="playedInstrument">The part the run's player played, if known.</param>
        /// <param name="stars">Band stars as reported by gameplay (gold and above count as 5).</param>
        /// <param name="scoreValid">
        /// False if the score was invalidated (e.g. reduced song speed, or no-fail toggled mid-song).
        /// An invalid score counts as a failed round.
        /// </param>
        public static void RecordSongEnd(RoguelikeContext context, Instrument? playedInstrument, int stars, bool scoreValid)
        {
            if (!Matches(context))
            {
                return;
            }

            if (!scoreValid)
            {
                YargLogger.LogInfo("Roguelike: score was invalidated, counting the round as failed.");
            }

            Record(playedInstrument, Math.Clamp(stars, 0, 5), failed: !scoreValid);
        }

        /// <summary>
        /// Record a song the player quit out of. Restarts are blocked in roguelike, so quitting is the
        /// only way out of a song that's going badly, and it counts as a failed round.
        /// </summary>
        public static void RecordQuit(RoguelikeContext context)
        {
            if (!Matches(context))
            {
                return;
            }

            YargLogger.LogInfo("Roguelike: song quit, counting the round as failed.");
            Record(null, 0, failed: true);
        }

        private static void Record(Instrument? playedInstrument, int stars, bool failed)
        {
            var run = Current;
            int roundNumber = run.Round + 1;
            var outcome = run.RecordResult(playedInstrument ?? run.PreferredInstrument, stars, failed);
            LastOutcome = outcome;

            YargLogger.LogFormatInfo("Roguelike: round {0} result: {1} stars, failed: {2} -> {3}",
                roundNumber, stars, failed, outcome);
        }
    }
}
