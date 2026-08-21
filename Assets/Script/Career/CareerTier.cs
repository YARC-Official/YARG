namespace YARG.Career
{
    public enum UnlockType
    {
        StarCount,
        CompletionCount
    }

    public enum CompletionBonusType
    {
        None,
        Image,
        Sound,
        Video,
    }

    // TODO: Move this to an appropriate location in YARG.Venue
    public enum VenueSize
    {
        NotSpecified,
        Starter,
        Small,
        Medium,
        Large,
        Epic
    }

    public class CareerTier
    {
        public string              Name;
        public string              Description;
        public CareerSong[]        Songs;
        public bool                CustomText;
        public string              CompletionBonusText;
        public CompletionBonusType CompletionBonus;
        public string              MediaFilename;
        public VenueSize           VenueSize;
        public string              VenueHint;

        public UnlockType          UnlockType;
        public int                 UnlockCriteria;

        public override string ToString()
        {
            return $"CareerTier: {Name}";
        }
    }
}