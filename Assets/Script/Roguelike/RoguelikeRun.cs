using System;
using System.Collections.Generic;
using YARG.Core;
using YARG.Core.Song;

namespace YARG.Roguelike
{
    public enum RoguelikeRunState
    {
        InProgress,
        Won,
        Lost,
    }

    public enum RoguelikeRoundOutcome
    {
        /// <summary>Round cleared, the run moves on to the next round.</summary>
        Advanced,

        /// <summary>Round not cleared, a life was spent and the round will be re-rolled.</summary>
        LifeLost,

        /// <summary>The boss round was cleared.</summary>
        RunWon,

        /// <summary>Round not cleared with no lives left.</summary>
        RunLost,
    }

    public sealed class RoguelikeRoundResult
    {
        public int         Round;
        public int         Attempt;
        public int         TargetIntensity;
        public HashWrapper SongHash;
        public Instrument  PlayedInstrument;
        public int         Stars;
        public bool        Failed;
        public bool        Passed;
    }

    /// <summary>
    /// State and rules for one roguelike run. This class is deliberately free of Unity and song
    /// library types: it only knows song hashes, so it can be unit tested and serialized on its own.
    /// Picking which songs to offer is done by <see cref="RoguelikeSongPicker"/>; the caller feeds the
    /// result back in through <see cref="SetChoices"/>.
    /// </summary>
    public sealed class RoguelikeRun
    {
        private readonly HashSet<HashWrapper>      _playedSongs = new();
        private readonly List<HashWrapper>         _choices     = new();
        private readonly List<RoguelikeRoundResult> _history     = new();

        public Guid           Id       { get; }
        public int            Seed     { get; }
        public RoguelikeRules Rules    { get; }

        /// <summary>The controller the run is played on; decides which parts can be substituted.</summary>
        public GameMode GameMode { get; }

        /// <summary>
        /// The part the player plays whenever a song charts it. See <see cref="RoguelikeParts"/> for
        /// how substitutes are handled.
        /// </summary>
        public Instrument PreferredInstrument { get; }

        /// <summary>Zero-based round index.</summary>
        public int Round { get; private set; }

        /// <summary>How many times the current round has been attempted before (0 on the first try).</summary>
        public int Attempt { get; private set; }

        public int               Lives { get; private set; }
        public RoguelikeRunState State { get; private set; } = RoguelikeRunState.InProgress;

        /// <summary>The song the player picked for the current round, or null before they pick.</summary>
        public HashWrapper? SelectedSong { get; private set; }

        public IReadOnlyList<HashWrapper>          Choices     => _choices;
        public IReadOnlyCollection<HashWrapper>    PlayedSongs => _playedSongs;
        public IReadOnlyList<RoguelikeRoundResult> History     => _history;

        public int  TargetIntensity => Rules.MinIntensity + Round;
        public bool IsBossRound     => Round == Rules.RoundCount - 1;
        public bool IsOver          => State != RoguelikeRunState.InProgress;

        /// <summary>True when the current round needs a fresh set of songs from the picker.</summary>
        public bool NeedsChoices => !IsOver && _choices.Count == 0;

        public RoguelikeRun(GameMode gameMode, Instrument preferredInstrument, int seed, RoguelikeRules rules = null)
        {
            Rules = rules ?? RoguelikeRules.Default;
            if (Rules.MaxIntensity < Rules.MinIntensity)
            {
                throw new ArgumentException("MaxIntensity must not be lower than MinIntensity.", nameof(rules));
            }
            if (Rules.ChoicesPerRound < 1)
            {
                throw new ArgumentException("ChoicesPerRound must be at least 1.", nameof(rules));
            }

            Id = Guid.NewGuid();
            Seed = seed;
            GameMode = gameMode;
            PreferredInstrument = preferredInstrument;
            Lives = Math.Max(1, Rules.StartingLives);
        }

        /// <summary>
        /// A random source for rolling the current round's choices. It is derived from the run seed,
        /// round and attempt, so the same run always offers the same songs for a given library, and
        /// re-entering the menu cannot be used to re-roll.
        /// </summary>
        public Random CreateRoundRandom()
        {
            unchecked
            {
                int hash = Seed;
                hash = hash * 31 + Round;
                hash = hash * 31 + Attempt;
                return new Random(hash);
            }
        }

        /// <summary>Store the songs offered for the current round.</summary>
        public void SetChoices(IEnumerable<HashWrapper> choices)
        {
            ThrowIfOver();

            _choices.Clear();
            foreach (var hash in choices)
            {
                if (!_choices.Contains(hash))
                {
                    _choices.Add(hash);
                }
            }
            SelectedSong = null;
        }

        /// <summary>Pick one of the current choices to play this round.</summary>
        public void SelectSong(HashWrapper song)
        {
            ThrowIfOver();

            if (!_choices.Contains(song))
            {
                throw new InvalidOperationException("The selected song is not one of this round's choices.");
            }
            SelectedSong = song;
        }

        /// <summary>
        /// Record the result of playing the selected song and advance the run accordingly.
        /// </summary>
        /// <param name="playedInstrument">The part actually played, kept for the run history.</param>
        /// <param name="stars">Band star count, 0-5 (gold counts as 5).</param>
        /// <param name="failed">True if the band failed out of the song.</param>
        public RoguelikeRoundOutcome RecordResult(Instrument playedInstrument, int stars, bool failed)
        {
            ThrowIfOver();

            if (SelectedSong is not { } song)
            {
                throw new InvalidOperationException("No song has been selected for this round.");
            }

            bool passed = !failed && stars >= Rules.MinStarsToPass;

            _playedSongs.Add(song);
            _history.Add(new RoguelikeRoundResult
            {
                Round = Round,
                Attempt = Attempt,
                TargetIntensity = TargetIntensity,
                SongHash = song,
                PlayedInstrument = playedInstrument,
                Stars = stars,
                Failed = failed,
                Passed = passed,
            });

            _choices.Clear();
            SelectedSong = null;

            if (passed)
            {
                if (IsBossRound)
                {
                    State = RoguelikeRunState.Won;
                    return RoguelikeRoundOutcome.RunWon;
                }

                Round++;
                Attempt = 0;
                return RoguelikeRoundOutcome.Advanced;
            }

            Lives--;
            if (Lives <= 0)
            {
                State = RoguelikeRunState.Lost;
                return RoguelikeRoundOutcome.RunLost;
            }

            Attempt++;
            return RoguelikeRoundOutcome.LifeLost;
        }

        /// <summary>End the run early, e.g. when the player quits out of a song or abandons the run.</summary>
        public void Forfeit()
        {
            if (!IsOver)
            {
                State = RoguelikeRunState.Lost;
                _choices.Clear();
                SelectedSong = null;
            }
        }

        private void ThrowIfOver()
        {
            if (IsOver)
            {
                throw new InvalidOperationException($"The run is already over ({State}).");
            }
        }
    }
}
