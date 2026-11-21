using System;
using System.Collections.Generic;
using YARG.Core.Logging;
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
        /// Whether outside apps may see the setlist: while the music library is the open menu, and through a show.
        /// Everywhere else YARG is about to clear the setlist before the player sees it again.
        /// </summary>
        public static bool IsActive => IsPlaying || _libraryOpen;

        private static bool _libraryOpen;

        /// <summary>
        /// Index into <see cref="ResolveSongs"/> of the song being played. Only meaningful while <see cref="IsPlaying"/>.
        /// </summary>
        public static int Index { get; private set; }

        /// <summary>
        /// Increments on every change to the songs, the playback position or <see cref="IsActive"/>. Never persisted.
        /// </summary>
        public static int Revision { get; private set; }

        /// <summary>
        /// Raised synchronously after every change. A handler that throws is logged and doesn't stop the others.
        /// During <see cref="TryStart"/> and <see cref="TryAdvance"/> it fires before the caller updates
        /// <c>GlobalVariables.State.CurrentSong</c>, so read the current song as <c>ResolveSongs()[Index]</c>.
        /// </summary>
        public static event Action Changed;

        public static bool HasNext => IsPlaying && Index + 1 < ResolveSongs().Count;

        /// <summary>
        /// Resolves the setlist's songs in the current library, allocating a new list. Hashes that don't resolve
        /// (for example, songs hidden by the content-rating settings) stay in <see cref="Playlist"/> but are skipped,
        /// until a sort rebuilds the playlist from this list and drops them.
        /// </summary>
        public static List<SongEntry> ResolveSongs() => Playlist.ToList();

        /// <summary>
        /// <see cref="ResolveSongs"/> into a list the caller reuses.
        /// </summary>
        public static void ResolveSongsInto(List<SongEntry> songs) => Playlist.ResolveInto(songs);

        /// <summary>
        /// Called by <c>MenuManager</c> whenever the menu stack changes.
        /// </summary>
        public static void SetLibraryOpen(bool open)
        {
            if (_libraryOpen == open)
            {
                return;
            }

            _libraryOpen = open;
            OnChanged();
        }

        /// <summary>
        /// Starts playing from the first song. Returns false, changing nothing, when no song resolves.
        /// </summary>
        public static bool TryStart(out SongEntry first)
        {
            var songs = ResolveSongs();
            if (songs.Count == 0)
            {
                first = null;
                return false;
            }

            IsPlaying = true;
            Index = 0;
            OnChanged();
            first = songs[0];
            return true;
        }

        public static bool TryAdvance(out SongEntry next)
        {
            next = null;
            if (!IsPlaying)
            {
                return false;
            }

            var songs = ResolveSongs();
            if (Index + 1 >= songs.Count)
            {
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

        /// <summary>
        /// Follows songs whose hash changed in a rescan (see <see cref="PlaylistContainer.ReplaceUpdatedSongHashes"/>).
        /// </summary>
        public static void ReplaceUpdatedSongHashes(IReadOnlyDictionary<HashWrapper, HashWrapper> replacements)
        {
            if (replacements == null || replacements.Count == 0)
            {
                return;
            }

            int replaced = PlaylistContainer.ReplaceHashes(Playlist, replacements);
            if (replaced == 0)
            {
                return;
            }

            // A song's new hash can already be in the setlist; AddSong never allows a song twice, so keep the first.
            var seen = new HashSet<HashWrapper>();
            int duplicates = Playlist.SongHashes.RemoveAll(hash => !seen.Add(hash));

            YargLogger.LogFormatInfo("Replaced {0} updated song hashes in the setlist, removing {1} duplicates",
                replaced, duplicates);
            OnChanged();
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
            if (Changed is null)
            {
                return;
            }

            // Changes happen inside scene loads and menu actions; one failing subscriber must not abort them.
            foreach (var handler in Changed.GetInvocationList())
            {
                try
                {
                    ((Action) handler)();
                }
                catch (Exception ex)
                {
                    YargLogger.LogException(ex, "Setlist.Changed handler failed");
                }
            }
        }
    }
}
