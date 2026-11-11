using YARG.Career;
using YARG.Core.Replays;
using YARG.Core.Song;
using YARG.Menu.ScoreScreen;
using YARG.Replays;
using YARG.Roguelike;

namespace YARG
{
    public struct PersistentState
    {
        public static PersistentState Default => new()
        {
            SongSpeed = 1f,
        };

        public SongEntry   CurrentSong;
#nullable enable
        public ReplayInfo? CurrentReplay;
#nullable disable

        public ScoreScreenStats? ScoreScreenStats;

        public float SongSpeed;

        // Non-null when the current song was started from a career (see CareerContext).
        public CareerContext CurrentCareer;

        // Non-null when the current song was started from a roguelike run (see RoguelikeContext).
        public RoguelikeContext CurrentRoguelike;

        public          bool    IsPractice;
        public readonly bool    IsReplay => CurrentReplay is not null;
        public          bool    PlayingWithReplay;
        public          double? SavedInputTime;
        public          bool    CrowdSfxVenueOverride;
    }
}