using System;
using System.Collections.Generic;
using YARG.Core;
using YARG.Core.Game;

namespace YARG.Scores
{
    // One player's score within a career completion commit.
    public class CareerPlayerScoreCommit
    {
        public Guid ProfileId;
        public int PlayerScoreRecordId;
        public Instrument Instrument;
        public Difficulty Difficulty;
        public StarAmount Stars;
        public int Score;
        public float Percent;
        public bool IsFc;
    }

    // Everything needed to commit a single career song completion (plus the scores that back it).
    // A score counts toward career only if it is linked by a committed career completion row.
    public class CareerSongCompletionCommit
    {
        public int CareerSaveId;
        public Guid CareerId;
        public Guid TierId;
        public int TierIndex;
        public Guid CareerSongId;
        public int SongIndex;
        public byte[] SongChecksum;
        public int GameRecordId;
        public int BandStars;
        public int BandScore;
        public DateTime CompletedAt;
        public CareerCompletionSource Source;
        public IReadOnlyList<CareerPlayerScoreCommit> PlayerScores;
    }
}
