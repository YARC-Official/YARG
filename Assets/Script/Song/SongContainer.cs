using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using YARG.Settings;
using YARG.Util;

namespace YARG.Song
{
    public static class SongContainer
    {
        private static readonly List<SongEntry> _songs;
        private static readonly Dictionary<string, SongEntry> _songsByHash;

        public static IReadOnlyList<SongEntry> Songs => _songs;
        public static IReadOnlyDictionary<string, SongEntry> SongsByHash => _songsByHash;

        static SongContainer()
        {
            _songs = new List<SongEntry>();
            _songsByHash = new Dictionary<string, SongEntry>();
        }

        public static void AddSongs(ICollection<SongEntry> songs)
        {
            _songs.AddRange(songs);

            foreach (var songEntry in songs)
            {
                if (_songsByHash.ContainsKey(songEntry.Checksum)) continue;

                _songsByHash.Add(songEntry.Checksum, songEntry);
            }

            TrySelectedSongReset();
        }

        public static async UniTask<List<CacheFolder>> ScanAllFolders(bool fast, Action<SongScanner> updateUi = null)
        {
            _songs.Clear();
            _songsByHash.Clear();

            // Add setlists as portable folder if installed
            IEnumerable<string> portableFolders = null;
            if (!string.IsNullOrEmpty(PathHelper.SetlistPath))
            {
                portableFolders = new[]
                {
                    PathHelper.SetlistPath
                };
            }

            var scanner = new SongScanner(SettingsManager.Settings.SongFolders, portableFolders);
            var output = await scanner.StartScan(fast, updateUi);

            AddSongs(output.SongEntries);
            return output.ErroredCaches;
        }

        public static async UniTask ScanFolders(ICollection<CacheFolder> folders, bool fast,
            Action<SongScanner> updateUi = null)
        {
            var songsToRemove = _songs.Where(song => folders.Any(i => i.Folder == song.CacheRoot)).ToList();

            _songs.RemoveAll(x => songsToRemove.Contains(x));
            foreach (var song in songsToRemove)
            {
                _songsByHash.Remove(song.Checksum);
            }

            var scanner = new SongScanner(folders);
            var songs = await scanner.StartScan(fast, updateUi);

            AddSongs(songs.SongEntries);
        }

        public static async UniTask ScanSingleFolder(string path, bool fast, Action<SongScanner> updateUi = null)
        {
            var songsToRemove = _songs.Where(song => song.CacheRoot == path).ToList();

            _songs.RemoveAll(x => songsToRemove.Contains(x));
            foreach (var song in songsToRemove)
            {
                _songsByHash.Remove(song.Checksum);
            }

            var scanner = new SongScanner(new[]
            {
                path
            });
            var songs = await scanner.StartScan(fast, updateUi);

            AddSongs(songs.SongEntries);
        }

        private static void TrySelectedSongReset()
        {
            if (GameManager.Instance.SelectedSong == null)
            {
                return;
            }

            if (!_songsByHash.ContainsKey(GameManager.Instance.SelectedSong.Checksum))
            {
                GameManager.Instance.SelectedSong = null;
            }
        }
    }
}