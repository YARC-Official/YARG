using System;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using YARG.Core.Logging;

namespace YARG.Career
{
    public enum CareerCompletionMode
    {
        SingleSong = 0,
        FullTierPlaylist = 1,
        FullCareerPlaylist = 2,
    }

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
        public string       Name;
        public Guid         Id;
        public CareerBase   Parent;
        public string       Description;
        public CareerSong[] Songs;
        public string       CustomUnlockText;
        public string       BackgroundImage;
        public string       CompletionBonusText;
        [JsonConverter(typeof(StringEnumConverter))]
        public CompletionBonusType CompletionBonus;
        public string              MediaFilename;
        [JsonConverter(typeof(StringEnumConverter))]
        public VenueSize           VenueSize;
        public string              VenueHint;

        [JsonConverter(typeof(StringEnumConverter))]
        public UnlockType          UnlockType;
        public int                 UnlockCriteria;

        // How this tier's songs are completed. Playlist modes can be specified in content but are not
        // yet honored by gameplay (single-song completion only for now).
        [JsonConverter(typeof(StringEnumConverter))]
        public CareerCompletionMode CompletionMode;

        // Bonus is not counted towards other tier unlocks
        public bool IsBonus;

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            if (Id == Guid.Empty)
            {
                YargLogger.LogFormatError(
                    "CareerTier: missing stable Id in content for tier '{0}'. Career progress will not be able to track this tier.",
                    Name);
            }
        }

        public CareerTier()
        {
        }

        public CareerTier(CareerTier other, CareerBase parent)
        {
            Name = other.Name;
            Id = Guid.NewGuid();
            Parent = parent;
            Description = other.Description;

            // TODO: This part is probably wrong
            Songs = new CareerSong[other.Songs.Length];
            Array.Copy(other.Songs, Songs, other.Songs.Length);

            BackgroundImage = other.BackgroundImage;
            CustomUnlockText = other.CustomUnlockText;
            CompletionBonusText = other.CompletionBonusText;
            CompletionBonus = other.CompletionBonus;
            MediaFilename = other.MediaFilename;
            VenueSize = other.VenueSize;
            VenueHint = other.VenueHint;
            UnlockType = other.UnlockType;
            UnlockCriteria = other.UnlockCriteria;
            IsBonus = other.IsBonus;
        }

        public override string ToString()
        {
            return $"CareerTier: {Name}";
        }

        public void RefreshSongEntries()
        {
            foreach (var song in Songs)
            {
                song.RefreshSongEntry();
            }
        }
    }
}