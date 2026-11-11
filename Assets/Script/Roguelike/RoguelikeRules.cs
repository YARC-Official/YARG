namespace YARG.Roguelike
{
    /// <summary>
    /// Tunable rules for a roguelike run. Kept separate from <see cref="RoguelikeRun"/> so they can
    /// later be exposed as presets or settings without touching the run logic.
    /// </summary>
    /// <remarks>
    /// Rounds map one-to-one onto song intensity tiers. Song intensity is a whole number from 0 to 6
    /// (6 being the "devil" tier), so the default run is seven rounds and the last one is the boss.
    /// </remarks>
    public sealed class RoguelikeRules
    {
        /// <summary>Intensity tier targeted by the first round.</summary>
        public int MinIntensity = 0;

        /// <summary>Intensity tier targeted by the final (boss) round.</summary>
        public int MaxIntensity = 6;

        /// <summary>How many songs the player is offered each round.</summary>
        public int ChoicesPerRound = 4;

        /// <summary>
        /// Failed rounds the player can absorb. When a round is failed with lives remaining, the same
        /// round is offered again with a fresh set of songs.
        /// </summary>
        public int StartingLives = 1;

        /// <summary>
        /// Band stars (0-5, gold counts as 5) needed to clear a round. Failing out of the song always
        /// loses the round regardless of this value.
        /// </summary>
        public int MinStarsToPass = 3;

        public int RoundCount => MaxIntensity - MinIntensity + 1;

        public static RoguelikeRules Default => new RoguelikeRules();
    }
}
