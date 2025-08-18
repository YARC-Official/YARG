using System;
using System.Collections.Generic;
using YARG.Core.Song;
using YARG.Song;

namespace YARG.Playlists
{
    public class Playlist
    {
        /// <summary>
        /// The display name of the playlist.
        /// </summary>
        public string Name;

        /// <summary>
        /// The author of the playlists.
        /// </summary>
        public string Author;

        /// <summary>
        /// The unique ID of the playlist.
        /// </summary>
        public Guid Id;

        /// <summary>
        /// The song hashes within the playlist.
        /// </summary>
        public List<HashWrapper> SongHashes;
        public readonly bool Ephemeral;

        public int Count => SongHashes.Count;


        public Playlist(bool ephemeral)
        {
            Ephemeral = ephemeral;
            SongHashes = new List<HashWrapper>();
            Author = "You";
            Name = "Setlist";
            Id = Guid.NewGuid();
        }

        public Playlist() {}

        public void AddSong(SongEntry song)
        {
            if (!ContainsSong(song))
            {
                SongHashes.Add(song.Hash);
                if (!Ephemeral)
                {
                    PlaylistContainer.SavePlaylist(this);
                }
            }

        }

        public void RemoveSong(SongEntry song)
        {
            SongHashes.Remove(song.Hash);
            if (!Ephemeral)
            {
                PlaylistContainer.SavePlaylist(this);
            }
        }

        public bool ContainsSong(SongEntry song)
        {
            return SongHashes.Contains(song.Hash);
        }

        public List<SongEntry> ToList()
        {
            var songlist = new List<SongEntry>();

            foreach (var hash in SongHashes)
            {
                if (SongContainer.SongsByHash.TryGetValue(hash, out var song))
                {
                    songlist.Add(song[0]);
                }
            }

            return songlist;
        }

        public void Clear()
        {
            SongHashes.Clear();
        }
    }
}