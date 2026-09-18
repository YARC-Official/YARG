using System;

namespace YARG.Career
{
    // Identifies the career context for an in-progress game. Carried from the career menu through
    // gameplay to the score-save path, so a score can be linked to a committed career completion.
    // The save itself is resolved at commit time from the set of players who actually played, so no
    // save ID is carried here.
    public class CareerContext
    {
        public Guid CareerId;
        public Guid TierId;
        public int TierIndex;
        public Guid CareerSongId;
        public int SongIndex;
    }
}
