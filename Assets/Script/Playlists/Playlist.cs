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

        /// <summary>
        /// Raised after each editing call on this class, even one that changes nothing (such as removing a song
        /// that isn't there); <see cref="Clear"/> raises it only when it removed something.
        /// Writes straight to <see cref="SongHashes"/> don't raise it.
        /// </summary>
        public event Action Edited;

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
                OnEdited();
            }
        }

        public void RemoveSong(SongEntry song)
        {
            SongHashes.Remove(song.Hash);
            OnEdited();
        }

        public bool ContainsSong(SongEntry song)
        {
            return SongHashes.Contains(song.Hash);
        }

        public List<SongEntry> ToList()
        {
            var songlist = new List<SongEntry>();
            ResolveInto(songlist);
            return songlist;
        }

        /// <summary>
        /// Replaces the contents of <paramref name="songs"/> with this playlist's songs that resolve in the current
        /// library, in order. Allocates only when <paramref name="songs"/> has to grow.
        /// </summary>
        public void ResolveInto(List<SongEntry> songs)
        {
            songs.Clear();

            foreach (var hash in SongHashes)
            {
                if (SongContainer.SongsByHash.TryGetValue(hash, out var song))
                {
                    songs.Add(song[0]);
                }
            }
        }

        public void MoveSongUp(SongEntry song)
        {
            // Get the index of the song
            var index = SongHashes.IndexOf(song.Hash);

            // If the song is not at the top, swap it with the previous song
            if (index > 0)
            {
                (SongHashes[index - 1], SongHashes[index]) = (SongHashes[index], SongHashes[index - 1]);
                OnEdited();
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
                OnEdited();
            }
        }

        public void Clear()
        {
            if (SongHashes.Count == 0)
            {
                return;
            }

            SongHashes.Clear();
            Edited?.Invoke();
        }

        public void SortByName(bool ascending = true)
        {
            // Get all songs, sort by name, then rebuild hash list
            var songs = ToList();
            songs.Sort((a, b) => CompareSongsByName(a, b, ascending));

            SongHashes.Clear();
            foreach (var song in songs)
            {
                SongHashes.Add(song.Hash);
            }

            OnEdited();
        }

        public void SortByArtist(bool ascending = true)
        {
            // Get all songs, sort by artist, then rebuild hash list
            var songs = ToList();
            songs.Sort((a, b) => CompareSongsByArtist(a, b, ascending));

            SongHashes.Clear();
            foreach (var song in songs)
            {
                SongHashes.Add(song.Hash);
            }

            OnEdited();
        }

        private void OnEdited()
        {
            if (!Ephemeral)
            {
                PlaylistContainer.RemoveDeadHashes(this, SongContainer.SongsByHash);
                PlaylistContainer.SavePlaylist(this);
            }

            Edited?.Invoke();
        }

        private static int CompareSongsByName(SongEntry lhs, SongEntry rhs, bool ascending)
        {
            int result = CompareSortStrings(lhs.Name, rhs.Name);
            if (result == 0)
            {
                result = CompareSortStrings(lhs.Artist, rhs.Artist);
            }
            if (result == 0)
            {
                result = lhs.SortBasedLocation.CompareTo(rhs.SortBasedLocation);
            }

            return ascending ? result : -result;
        }

        private static int CompareSongsByArtist(SongEntry lhs, SongEntry rhs, bool ascending)
        {
            int result = CompareSortStrings(lhs.Artist, rhs.Artist);
            if (result == 0)
            {
                result = CompareSortStrings(lhs.Name, rhs.Name);
            }
            if (result == 0)
            {
                result = lhs.SortBasedLocation.CompareTo(rhs.SortBasedLocation);
            }

            return ascending ? result : -result;
        }

        private static int CompareSortStrings(SortString lhs, SortString rhs)
        {
            int result = lhs.CompareTo(rhs);
            if (result == 0)
            {
                result = string.Compare(lhs.Original, rhs.Original, StringComparison.OrdinalIgnoreCase);
            }

            return result;
        }
    }
}
