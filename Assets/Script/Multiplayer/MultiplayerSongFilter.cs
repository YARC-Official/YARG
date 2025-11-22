using System;
using System.Collections.Generic;
using System.Linq;
using YARG.Core.Logging;
using YARG.Core.Song;
using YARG.Menu.MusicLibrary;
using YARG.Song;

namespace YARG.Multiplayer
{
    public static class MultiplayerSongFilter
    {
        private static HashSet<HashWrapper>? _sharedSongs;
        private static List<byte>? _incomingBuffer;

        public static event Action SharedSongsUpdated;

        public static bool IsActive => _sharedSongs != null;

        public static IReadOnlyCollection<HashWrapper>? SharedSongs => _sharedSongs;

        public static void BeginSharedSongsUpload()
        {
            _incomingBuffer = new List<byte>();
        }

        public static void AppendSharedSongsChunk(byte[] chunk)
        {
            if (chunk == null || chunk.Length == 0)
            {
                return;
            }

            _incomingBuffer ??= new List<byte>();
            _incomingBuffer.AddRange(chunk);
        }

        public static void CommitSharedSongsUpload()
        {
            if (_incomingBuffer == null)
            {
                SetSharedSongs(Array.Empty<HashWrapper>());
                return;
            }

            var buffer = _incomingBuffer;
            _incomingBuffer = null;

            int hashSize = HashWrapper.HASH_SIZE_IN_BYTES;
            if (buffer.Count % hashSize != 0)
            {
                YargLogger.LogWarning("Received shared song data with invalid length; clearing filter.");
                SetSharedSongs(Array.Empty<HashWrapper>());
                return;
            }

            var bytes = buffer.ToArray();
            var hashes = new HashSet<HashWrapper>(bytes.Length / hashSize);
            for (int offset = 0; offset < bytes.Length; offset += hashSize)
            {
                var hash = HashWrapper.Create(new ReadOnlySpan<byte>(bytes, offset, hashSize));
                hashes.Add(hash);
            }

            SetSharedSongs(hashes);
        }

        public static void SetSharedSongs(IEnumerable<HashWrapper> hashes)
        {
            if (hashes is HashSet<HashWrapper> hashSet)
            {
                _sharedSongs = new HashSet<HashWrapper>(hashSet);
            }
            else
            {
                _sharedSongs = new HashSet<HashWrapper>(hashes);
            }

            MusicLibraryMenu.SetReload(MusicLibraryReloadState.Partial);
            SharedSongsUpdated?.Invoke();
        }

        public static void ClearSharedSongs()
        {
            if (_sharedSongs == null && _incomingBuffer == null)
            {
                return;
            }

            _sharedSongs = null;
            _incomingBuffer = null;
            MusicLibraryMenu.SetReload(MusicLibraryReloadState.Partial);
            SharedSongsUpdated?.Invoke();
        }

        public static bool IsSongAllowed(SongEntry song)
        {
            return _sharedSongs == null || _sharedSongs.Contains(song.Hash);
        }

        public static SongEntry[] FilterSongs(IEnumerable<SongEntry> songs)
        {
            if (_sharedSongs == null)
            {
                return songs as SongEntry[] ?? songs.ToArray();
            }

            return songs.Where(song => _sharedSongs.Contains(song.Hash)).ToArray();
        }

        public static SongCategory[] FilterCategories(IEnumerable<SongCategory> categories)
        {
            if (_sharedSongs == null)
            {
                return categories as SongCategory[] ?? categories.ToArray();
            }

            var filteredCategories = new List<SongCategory>();
            foreach (var category in categories)
            {
                var filteredSongs = category.Songs.Where(song => _sharedSongs.Contains(song.Hash)).ToArray();
                if (filteredSongs.Length > 0)
                {
                    filteredCategories.Add(new SongCategory(category.Category, filteredSongs, category.CategoryGroup));
                }
            }

            return filteredCategories.ToArray();
        }
    }
}
