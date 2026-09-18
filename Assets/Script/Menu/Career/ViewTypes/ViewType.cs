using YARG.Menu.ListMenu;

namespace YARG.Menu.Career
{
    public abstract class ViewType : BaseViewType
    {
        // The star display in the menus has five slots, so a single career song is worth at most
        // five stars for progress purposes (see StarView.SetStars).
        public const int STARS_PER_SONG = 5;

        /// <summary>
        /// Which layout a row of the career list uses. One pool object (SongView) renders every kind,
        /// because ListMenu instantiates a single prefab for all rows.
        /// </summary>
        public enum CareerRowKind
        {
            /// <summary>Progress header: who is playing, the current tier, and star totals.</summary>
            CareerHeader,

            /// <summary>Header of an unlocked tier: name plus "earned / available" stars.</summary>
            TierHeader,

            /// <summary>Header of the next locked tier: name plus what is still required. Never clickable.</summary>
            LockedTierHeader,

            /// <summary>A song row.</summary>
            Song,
        }

        /// <summary>
        /// Row data that the primary/secondary text pair cannot express on its own (stars, detail
        /// line, lock state). Populated per row kind; unused fields stay at their default.
        /// </summary>
        public struct CareerInfo
        {
            public CareerRowKind Kind;

            public bool Unlocked;
            public bool Completed;

            // Song rows: best stars earned on this song, 0 when it has never been completed.
            public int BestStars;

            // Header rows: stars earned and stars available within the row's scope
            // (a tier header counts its own songs, the career header counts the current tier).
            public int EarnedStars;
            public int AvailableStars;

            // Career header only: the same pair measured over the whole run, all tiers included.
            public int TotalEarnedStars;
            public int TotalAvailableStars;

            // Career header: the seated players' names, and the tier the run is currently on.
            public string PlayerNames;
            public string TierName;

            // Song rows: the library song this career song maps to.
            public string Artist;
            public string Album;

            // Locked tier header: what is still required to unlock the tier.
            public string LockHint;

            public readonly bool IsHeader => Kind != CareerRowKind.Song;
        }

        /// <summary>
        /// Extra data for the row layout. Null means "plain list item", so rows that do not opt in
        /// keep rendering from GetPrimaryText/GetSecondaryText only.
        /// </summary>
        public virtual CareerInfo? GetCareerInfo()
        {
            return null;
        }

        /// <summary>
        /// Whether confirming on this row does anything. Locked tiers stay selectable for browsing,
        /// but are inert.
        /// </summary>
        public virtual bool IsClickable => true;

        public virtual void ViewClick()
        {

        }
    }
}
