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

        public void MoveSongUp(SongEntry song)
        {
            // Get the index of the song
            var index = SongHashes.IndexOf(song.Hash);

            // If the song is not at the top, swap it with the previous song
            if (index > 0)
            {
                (SongHashes[index - 1], SongHashes[index]) = (SongHashes[index], SongHashes[index - 1]);
                PlaylistContainer.SavePlaylist(this);
            }
        }

        public void MoveSongDown(SongEntry song)
        {
            // Get the index of the song
            var index = SongHashes.IndexOf(song.Hash);

            // If the song is not at the bottom, swap it with the next song
            if (index < SongHashes.Count - 1)
            {
                (SongHashes[index + 1], SongHashes[index]) = (SongHashes[index], SongHashes[index + 1]);
                PlaylistContainer.SavePlaylist(this);
            }
        }

        public void Clear()
        {
            SongHashes.Clear();
        }

        public void SortByName(bool ascending = true)
        {
            // Get all songs, sort by name, then rebuild hash list
            var songs = ToList();

            if (ascending)
            {
                songs.Sort((a, b) => string.Compare(a.Name, b.Name, System.StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                songs.Sort((a, b) => string.Compare(b.Name, a.Name, System.StringComparison.OrdinalIgnoreCase));
            }

            SongHashes.Clear();
            foreach (var song in songs)
            {
                SongHashes.Add(song.Hash);
            }

            if (!Ephemeral)
            {
                PlaylistContainer.SavePlaylist(this);
            }
        }

        public void SortByArtist(bool ascending = true)
        {
            // Get all songs, sort by artist, then rebuild hash list
            var songs = ToList();

            if (ascending)
            {
                songs.Sort((a, b) => string.Compare(a.Artist, b.Artist, System.StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                songs.Sort((a, b) => string.Compare(b.Artist, a.Artist, System.StringComparison.OrdinalIgnoreCase));
            }

            SongHashes.Clear();
            foreach (var song in songs)
            {
                SongHashes.Add(song.Hash);
            }

            if (!Ephemeral)
            {
                PlaylistContainer.SavePlaylist(this);
            }
        }
    }
}