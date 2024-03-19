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
        };

        public SongEntry   CurrentSong;
        public ReplayEntry CurrentReplay;

        public ScoreScreenStats? ScoreScreenStats;

        public float SongSpeed;

        public bool IsPractice;
        public bool IsReplay => CurrentReplay is not null;
    }
}