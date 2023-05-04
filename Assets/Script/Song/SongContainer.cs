using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using YARG.Settings;

namespace YARG.Song {
	public static class SongContainer {

		public static string CacheFolder => Path.Combine(GameManager.PersistentDataPath, "caches");

		private static readonly List<SongEntry> _songs;
		private static readonly Dictionary<string, SongEntry> _songsByHash;

		public static string[] SongFolders => SettingsManager.Settings.SongFolders;
		public static string[] SongUpgradeFolders => SettingsManager.Settings.SongUpgradeFolders;

		public static IReadOnlyList<SongEntry> Songs => _songs;
		public static IReadOnlyDictionary<string, SongEntry> SongsByHash => _songsByHash;

		static SongContainer() {
			_songs = new List<SongEntry>();
			_songsByHash = new Dictionary<string, SongEntry>();
		}

		public static void AddSongs(ICollection<SongEntry> songs) {
			_songs.AddRange(songs);

			foreach (var songEntry in songs) {
				if (_songsByHash.ContainsKey(songEntry.Checksum))
					continue;

				_songsByHash.Add(songEntry.Checksum, songEntry);
			}
		}

		public static async UniTask ScanAllFolders(bool fast, Action<SongScanner> updateUi = null) {
			_songs.Clear();
			_songsByHash.Clear();

			var scanner = new SongScanner(SongFolders);
			var songs = await scanner.StartScan(fast, updateUi);

			AddSongs(songs);
		}

		public static async UniTask ScanFolder(string path) {
			var songsToRemove = _songs.Where(song => song.CacheRoot == path).ToList();

			_songs.RemoveAll(x => x.CacheRoot == path);
			foreach (var song in songsToRemove) {
				_songsByHash.Remove(song.Checksum);
			}

			var scanner = new SongScanner(new[] { path });
			var songs = await scanner.StartScan(false, null);

			AddSongs(songs);
		}

	}
}