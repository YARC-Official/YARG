using System;
using System.Collections.Generic;
using System.Linq;
using YARG.Core.Song;

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

        public void AddSong(SongEntry song)
        {
            if (!ContainsSong(song))
            {
                SongHashes.Add(song.Hash);
                PlaylistContainer.SavePlaylist(this);
            }

        }

        public void RemoveSong(SongEntry song)
        {
            SongHashes.Remove(song.Hash);
            PlaylistContainer.SavePlaylist(this);
        }

        public bool ContainsSong(SongEntry song)
        {
            return SongHashes.Contains(song.Hash);
        }
    }
}