using UnityEngine;

namespace YARG.Venue
{
    public class VenueMetadata : ScriptableObject
    {
        public string VenueAddress;
        public string DisplayName;
        public string Author;

        [TextArea(2, 5)]
        public string Description;

        public Sprite PreviewSprite;

        public bool ExcludeFromRandomByDefault;
    }

    public static class VenueAddressablesConstants
    {
        public const string VenueLabel         = "venue";
        public const string VenueMetadataLabel = "venue-meta";
        public const string VenueNoRandomLabel = "venue-no-random";
    }
}
