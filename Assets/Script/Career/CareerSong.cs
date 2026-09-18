using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
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
        // Stable identity for this career song, authored in content. Used to link progress rows.
        public Guid Id;

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
            Id = songId;
            Identifier = CareerSongIdentifier.SongId;
            SongId = songId;
            SongEntry = GetSongEntry();
        }

        public CareerSong(HashWrapper songHash)
        {
            // We're using md5 here because it happens to generate 16 bytes, so we can make it into a stable guid
            var md5 = MD5.Create();
            Id = new Guid(md5.ComputeHash(songHash.HashBytes));
            Identifier = CareerSongIdentifier.SongHash;
            SongHash = songHash;
            SongEntry = GetSongEntry();
        }

        public CareerSong(string shortName)
        {
            var md5 = MD5.Create();
            Id = new Guid(md5.ComputeHash(Encoding.UTF8.GetBytes(shortName)));
            Identifier = CareerSongIdentifier.ShortName;
            ShortName = shortName;
            SongEntry = GetSongEntry();
        }

        public CareerSong(SongTuple songTuple)
        {
            var md5 = MD5.Create();
            Id = new Guid(md5.ComputeHash(Encoding.UTF8.GetBytes(songTuple.ToString())));
            Identifier = CareerSongIdentifier.SongTuple;
            SongTuple = songTuple;
            SongEntry = GetSongEntry();
        }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            if (Id == Guid.Empty)
            {
                YargLogger.LogError("CareerSong: missing stable Id in content. Career progress will not be able to link this song.");
            }

            SongEntry = GetSongEntry();
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
            if (!tuple.HasValue)
            {
                return null;
            }

            var identifier = tuple.Value;

            if (string.IsNullOrEmpty(identifier.Title) ||
                string.IsNullOrEmpty(identifier.Artist) ||
                string.IsNullOrEmpty(identifier.Source) ||
                string.IsNullOrEmpty(identifier.Charter))
            {
                return null;
            }

            SortString sourceSort = new SortString(identifier.Source);

            if (!SongContainer.Sources.TryGetValue(sourceSort, out var sourceMatches))
            {
                return null;
            }

            var matches = new List<SongEntry>();
            for (var i = 0; i < sourceMatches.Count; i++)
            {
                var song = sourceMatches[i];
                if (song.Artist == identifier.Artist &&
                    song.Name == identifier.Title &&
                    song.Charter == identifier.Charter)
                {
                    matches.Add(song);
                }
            }

            if (matches.Count == 0)
            {
                return null;
            }

            if (matches.Count > 1)
            {
                YargLogger.LogFormatWarning<string,string,string,string>("Multiple songs found matching tuple {0}, {1}, {2}, {3}",
                    identifier.Artist, identifier.Source, identifier.Charter, identifier.Title);
            }

            return matches[0];
        }
    }
}