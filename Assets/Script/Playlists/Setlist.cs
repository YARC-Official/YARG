using System;
using System.Collections.Generic;
using YARG.Core.Song;

namespace YARG.Playlists
{
    /// <summary>
    /// The live setlist: the songs queued in the music library, and the position in them while a show is played.
    /// </summary>
    public static class Setlist
    {
        public static Playlist Playlist { get; } = CreatePlaylist();

        public static bool IsPlaying { get; private set; }

        /// <summary>
        /// Index into <see cref="Songs"/> of the song being played. Only meaningful while <see cref="IsPlaying"/>.
        /// </summary>
        public static int Index { get; private set; }

        /// <summary>
        /// Increments on every change to the songs or the playback position. Never persisted.
        /// </summary>
        public static int Revision { get; private set; }

        public static event Action Changed;

        /// <summary>
        /// The setlist's songs that resolve in the current library. Hashes that don't resolve stay in
        /// <see cref="Playlist"/> but are skipped, so a song hidden by a filter comes back when it's shown again.
        /// </summary>
        public static List<SongEntry> Songs => Playlist.ToList();

        public static bool HasNext => IsPlaying && Index + 1 < Songs.Count;

        /// <summary>
        /// Starts playing from the first song and returns it. The caller must check that <see cref="Songs"/> isn't empty.
        /// </summary>
        public static SongEntry Start()
        {
            IsPlaying = true;
            Index = 0;
            OnChanged();
            return Songs[0];
        }

        public static bool TryAdvance(out SongEntry next)
        {
            var songs = Songs;
            if (!IsPlaying || Index + 1 >= songs.Count)
            {
                next = null;
                return false;
            }

            Index++;
            OnChanged();
            next = songs[Index];
            return true;
        }

        public static void EndShow()
        {
            if (!IsPlaying)
            {
                return;
            }

            IsPlaying = false;
            Index = 0;
            OnChanged();
        }

        public static void Reset()
        {
            EndShow();
            Playlist.Clear();
        }

        private static Playlist CreatePlaylist()
        {
            var playlist = new Playlist(true);
            playlist.Edited += OnChanged;
            return playlist;
        }

        private static void OnChanged()
        {
            Revision++;
            Changed?.Invoke();
        }
    }
}
