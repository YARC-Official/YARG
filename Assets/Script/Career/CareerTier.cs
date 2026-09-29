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

        public CareerTier(CareerTier other)
        {
            Name = other.Name;
            Id = Guid.NewGuid();
            Description = other.Description;

            if (other.Songs != null)
            {
                Songs = new CareerSong[other.Songs.Length];
                for (int i = 0; i < other.Songs.Length; i++)
                {
                    Songs[i] = new CareerSong(other.Songs[i]) { Parent = this };
                }
            }
            else
            {
                Songs = Array.Empty<CareerSong>();
            }

            BackgroundImage = other.BackgroundImage;
            CustomUnlockText = other.CustomUnlockText;
            CompletionBonusText = other.CompletionBonusText;
            CompletionBonus = other.CompletionBonus;
            MediaFilename = other.MediaFilename;
            VenueSize = other.VenueSize;
            VenueHint = other.VenueHint;
            UnlockType = other.UnlockType;
            UnlockCriteria = other.UnlockCriteria;
            CompletionMode = other.CompletionMode;
            IsBonus = other.IsBonus;
        }

        public void AddSong(CareerSong song)
        {
            if (song == null) return;
            song.Parent = this;
            var list = Songs != null ? new System.Collections.Generic.List<CareerSong>(Songs) : new System.Collections.Generic.List<CareerSong>();
            list.Add(song);
            Songs = list.ToArray();
        }

        public void InsertSong(int index, CareerSong song)
        {
            if (song == null) return;
            song.Parent = this;
            var list = Songs != null ? new System.Collections.Generic.List<CareerSong>(Songs) : new System.Collections.Generic.List<CareerSong>();
            if (index < 0) index = 0;
            if (index > list.Count) index = list.Count;
            list.Insert(index, song);
            Songs = list.ToArray();
        }

        public bool RemoveSong(CareerSong song)
        {
            if (Songs == null) return false;
            var list = new System.Collections.Generic.List<CareerSong>(Songs);
            bool removed = list.Remove(song);
            if (removed)
            {
                Songs = list.ToArray();
            }
            return removed;
        }

        public void RemoveSongAt(int index)
        {
            if (Songs == null || index < 0 || index >= Songs.Length) return;
            var list = new System.Collections.Generic.List<CareerSong>(Songs);
            list.RemoveAt(index);
            Songs = list.ToArray();
        }

        public void MoveSong(int fromIndex, int toIndex)
        {
            if (Songs == null || fromIndex < 0 || fromIndex >= Songs.Length || toIndex < 0 || toIndex >= Songs.Length || fromIndex == toIndex)
                return;

            var list = new System.Collections.Generic.List<CareerSong>(Songs);
            var song = list[fromIndex];
            list.RemoveAt(fromIndex);
            list.Insert(toIndex, song);
            Songs = list.ToArray();
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