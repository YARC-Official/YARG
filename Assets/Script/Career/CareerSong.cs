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
        // Should be set when song can no longer improve unlock criteria
        // e.g. if next tier criteria is completion, when song has been finished, or if next tier criteria is
        // stars, when player has achieved 5 stars on this song
        [NonSerialized]
        public bool IsCompleted;

        public CareerSong()
        {
        }

        public CareerSong(CareerSong other)
        {
            Id = Guid.NewGuid();
            Identifier = other.Identifier;
            SongId = other.SongId;
            SongHash = other.SongHash;
            ShortName = other.ShortName;
            SongTuple = other.SongTuple;
            Description = other.Description;
            SongEntry = other.SongEntry;
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
            if (SongContainer.Count == 0)
            {
                // Either SongContainer has not yet been initialized or has no songs, so don't bother trying
                return;
            }
            if (Id == Guid.Empty)
            {
                YargLogger.LogError("CareerSong: missing stable Id in content. Career progress will not be able to link this song.");
            }

            RefreshSongEntry();
        }

        public void RefreshSongEntry()
        {
            SongEntry = GetSongEntry();
        }

        public static CareerSong FromSongEntry(SongEntry song, CareerSongIdentifier identifier = CareerSongIdentifier.SongId)
        {
            if (song == null) return null;

            switch (identifier)
            {
                case CareerSongIdentifier.SongHash:
                    return new CareerSong(song.Hash);
                case CareerSongIdentifier.ShortName:
                    return new CareerSong(song.Name);
                case CareerSongIdentifier.SongTuple:
                    return new CareerSong(new SongTuple
                    {
                        Artist = song.Artist.Original,
                        Title = song.Name.Original,
                        Source = song.Source.Original,
                        Charter = song.Charter.Original
                    });
                case CareerSongIdentifier.SongId:
                default:
                    Guid guid = Guid.Empty;
                    foreach (var pair in SongContainer.SongsByGuid)
                    {
                        if (pair.Value != null && pair.Value.Contains(song))
                        {
                            guid = pair.Key;
                            break;
                        }
                    }
                    if (guid == Guid.Empty)
                    {
                        using var md5 = MD5.Create();
                        guid = new Guid(md5.ComputeHash(song.Hash.HashBytes));
                    }
                    var careerSong = new CareerSong(guid);
                    careerSong.SongEntry = song;
                    return careerSong;
            }
        }

        public void UpdateIdentifier(CareerSongIdentifier newIdentifier, SongEntry entry = null)
        {
            var song = entry ?? SongEntry;
            Identifier = newIdentifier;
            if (song == null) return;

            SongId = null;
            SongHash = null;
            ShortName = null;
            SongTuple = null;

            switch (newIdentifier)
            {
                case CareerSongIdentifier.SongId:
                    Guid guid = Guid.Empty;
                    foreach (var pair in SongContainer.SongsByGuid)
                    {
                        if (pair.Value != null && pair.Value.Contains(song))
                        {
                            guid = pair.Key;
                            break;
                        }
                    }
                    if (guid == Guid.Empty)
                    {
                        using var md5 = MD5.Create();
                        guid = new Guid(md5.ComputeHash(song.Hash.HashBytes));
                    }
                    SongId = guid;
                    Id = guid;
                    break;
                case CareerSongIdentifier.SongHash:
                    SongHash = song.Hash;
                    using (var md5 = MD5.Create())
                    {
                        Id = new Guid(md5.ComputeHash(song.Hash.HashBytes));
                    }
                    break;
                case CareerSongIdentifier.ShortName:
                    ShortName = song.Name;
                    using (var md5 = MD5.Create())
                    {
                        Id = new Guid(md5.ComputeHash(Encoding.UTF8.GetBytes(song.Name)));
                    }
                    break;
                case CareerSongIdentifier.SongTuple:
                    SongTuple = new SongTuple
                    {
                        Artist = song.Artist.Original,
                        Title = song.Name.Original,
                        Source = song.Source.Original,
                        Charter = song.Charter.Original
                    };
                    using (var md5 = MD5.Create())
                    {
                        Id = new Guid(md5.ComputeHash(Encoding.UTF8.GetBytes(SongTuple.Value.ToString())));
                    }
                    break;
            }
            SongEntry = song;
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