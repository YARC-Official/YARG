using System.Collections.Generic;
using YARG.Career;
using YARG.Core.Replays;
using YARG.Core.Song;
using YARG.Menu.ScoreScreen;
using YARG.Replays;

namespace YARG
{
    public struct PersistentState
    {
        public static PersistentState Default => new()
        {
            SongSpeed = 1f,
            ShowSongs = new List<SongEntry>(),
        };

        public SongEntry   CurrentSong;
#nullable enable
        public ReplayInfo? CurrentReplay;
#nullable disable

        public ScoreScreenStats? ScoreScreenStats;

        public float SongSpeed;

        public          bool            PlayingAShow { get; set; }
        public          List<SongEntry> ShowSongs    { get; set; }
        public          int             ShowIndex    { get; set; }

        // Non-null when the current song was started from a career (see CareerContext).
        public CareerContext CurrentCareer;

        public          bool    IsPractice;
        public readonly bool    IsReplay => CurrentReplay is not null;
        public          bool    PlayingWithReplay;
        public          double? SavedInputTime;
        public          bool    CrowdSfxVenueOverride;
    }
}