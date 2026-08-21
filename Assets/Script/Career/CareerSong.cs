using System;
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
        public CareerSongIdentifier Identifier;

        public CareerSong(Guid songId)
        {
            Identifier = CareerSongIdentifier.SongId;
            SongId = songId;
        }

        public CareerSong(HashWrapper songHash)
        {
            Identifier = CareerSongIdentifier.SongHash;
            SongHash = songHash;
        }

        public CareerSong(string shortName)
        {
            Identifier = CareerSongIdentifier.ShortName;
            ShortName = shortName;
        }

        public CareerSong(SongTuple songTuple)
        {
            Identifier = CareerSongIdentifier.SongTuple;
            SongTuple = songTuple;
        }

        public SongEntry GetSongEntry()
        {
            SongEntry entry = null;
            switch (Identifier)
            {
                // Doesn't exist yet
                case CareerSongIdentifier.SongId:
                    break;
                case CareerSongIdentifier.SongHash:
                    if (SongHash.HasValue)
                    {
                        // Arbitrarily choose the first song with this hash
                        entry = SongContainer.SongsByHash[SongHash.Value][0];
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