using System;
using SQLite;

namespace YARG.Scores
{
    [Table("CareerSongCompletions")]
    public class CareerSongCompletions
    {
        [PrimaryKey][AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public int CareerSaveId { get; set; }
        [Indexed]
        public Guid CareerId { get; set; }

        public Guid TierId { get; set; }
        // I don't think we actually need this
        public int TierIndex { get; set; }

        // TODO: Figure out how better to uniquely identify a career song
        public Guid CareerSongId { get; set; }
        // Again, why?
        public int SongIndex { get; set; }

        public byte[] SongChecksum { get; set; }
        public Guid GameRecordId { get; set; }

        public int BandStars { get; set; }
        public int BandScore { get; set; }

        public DateTime CompletedAt { get; set; }
        // Could be the kind of completion (single song, tier playlist, debug grant)
        // public int Source { get; set; }
    }
}