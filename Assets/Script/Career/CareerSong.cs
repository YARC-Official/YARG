using System;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using YARG.Core.Logging;
using YARG.Core.Song;
using YARG.Song;

namespace YARG.Career
{
    public struct SongTuple
    {
        public string Artist;
        public string Title;
        public string Source;
        public string Charter;

        public override string ToString()
        {
            return $"{Artist} - {Title} - {Source} - {Charter}";
        }
    }

    public enum CareerSongIdentifier
    {
        SongId,
        SongHash,
        ShortName,
        SongTuple
    }

    public class CareerSong
    {
        // Possible identifiers, choose one
        public Guid?        SongId;
        public HashWrapper? SongHash;
        public string?      ShortName;
        public SongTuple?   SongTuple;

        // Which identifier this particular instance is using
        [JsonConverter(typeof(StringEnumConverter))]
        public CareerSongIdentifier Identifier;

        // Not required, only shown if the song can't be found in the library
        public string Description;

        [NonSerialized]
        public SongEntry SongEntry;
        [NonSerialized]
        public CareerTier Parent;

        public CareerSong()
        {
        }

        public CareerSong(Guid songId)
        {
            Identifier = CareerSongIdentifier.SongId;
            SongId = songId;
            SongEntry = GetSongEntry();
        }

        public CareerSong(HashWrapper songHash)
        {
            Identifier = CareerSongIdentifier.SongHash;
            SongHash = songHash;
            SongEntry = GetSongEntry();
        }

        public CareerSong(string shortName)
        {
            Identifier = CareerSongIdentifier.ShortName;
            ShortName = shortName;
            SongEntry = GetSongEntry();
        }

        public CareerSong(SongTuple songTuple)
        {
            Identifier = CareerSongIdentifier.SongTuple;
            SongTuple = songTuple;
            SongEntry = GetSongEntry();
        }

        [OnDeserialized]
        private SongEntry OnDeserialized(StreamingContext context)
        {
            return GetSongEntry();
        }

        private SongEntry GetSongEntry()
        {
            SongEntry entry = null;
            switch (Identifier)
            {
                // Doesn't exist yet
                case CareerSongIdentifier.SongId:
                    // Arbitrarily choose the first song with this GUID
                    if (SongId.HasValue)
                    {
                        if (SongContainer.SongsByGuid.TryGetValue(SongId.Value, out var idEntries) && idEntries.Count > 0)
                        {
                            entry = idEntries[0];
                        }
                        else
                        {
                            YargLogger.LogFormatError("CareerSong: Unable to find song with ID {0}", SongId.Value);
                        }
                    }
                    else
                    {
                        YargLogger.LogError("CareerSong: Song ID was null!");
                    }

                    break;
                case CareerSongIdentifier.SongHash:
                    if (SongHash.HasValue)
                    {
                        if (SongContainer.SongsByHash.TryGetValue(SongHash.Value, out var hashEntries) && hashEntries.Count > 0) {
                            // Arbitrarily choose the first song with this hash
                            entry = hashEntries[0];
                        }
                        else
                        {
                            YargLogger.LogFormatError("CareerSong: Unable to find song with hash {0}", SongHash.Value);
                        }
                    }
                    else
                    {
                        YargLogger.LogError("CareerSong: Song hash was null!");
                    }

                    break;
                case CareerSongIdentifier.ShortName:
                    break;
                case CareerSongIdentifier.SongTuple:
                    entry = FindSongByTuple(SongTuple);
                    if (entry == null)
                    {
                        YargLogger.LogFormatError("CareerSong: Unable to find song with tuple {0}", SongTuple);
                    }
                    break;
            }

            return entry;
        }

        private static SongEntry FindSongByTuple(SongTuple? tuple)
        {
            // TODO: This is spaghetti, clean it up

            if (!tuple.HasValue)
            {
                return null;
            }

            var identifier = tuple.Value;

            // Title seems most specific, so get all the songs with the given title
            if (!SongContainer.Titles.TryGetValue(identifier.Title, out var songs))
            {
                return null;
            }

            // Filter down to the songs matching the artist
            if (string.IsNullOrEmpty(identifier.Artist))
            {
                return null;
            }

            for (var i = songs.Count - 1; i >= 0; i--)
            {
                if (songs[i].Artist != identifier.Artist)
                {
                    songs.RemoveAt(i);
                }
            }

            if (songs.Count == 0)
            {
                return null;
            }

            // Now do source
            if (string.IsNullOrEmpty(identifier.Source))
            {
                return null;
            }

            for (var i = songs.Count - 1; i >= 0; i--)
            {
                if (songs[i].Source != identifier.Source)
                {
                    songs.RemoveAt(i);
                }
            }

            if (songs.Count == 0)
            {
                return null;
            }

            // Finally, charter - hopefully this gives us a unique result
            if (string.IsNullOrEmpty(identifier.Charter))
            {
                return null;
            }

            for (var i = songs.Count - 1; i >= 0; i--)
            {
                if (songs[i].Charter != identifier.Charter)
                {
                    songs.RemoveAt(i);
                }
            }

            if (songs.Count == 0)
            {
                return null;
            }

            if (songs.Count > 1)
            {
                YargLogger.LogFormatWarning<string,string,string,string>("Multiple songs found matching tuple {0}, {1}, {2}, {3}",
                    identifier.Artist, identifier.Source, identifier.Charter, identifier.Title);
            }

            return songs[0];
        }
    }
}