using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using YARG.Data;
using YARG.UI;
using XboxSTFS;

namespace YARG.Song {
	public class SongCache {

		/// <summary>
		/// The date revision of the cache format.
		/// Format is YY_MM_DD_RR: Y = year, M = month, D = day, R = revision (reset across dates, only increment
		/// if multiple cache version changes happen in a single day).
		/// </summary>
		private const int CACHE_VERSION = 23_06_05_01;

		private readonly string _folder;
		private readonly string _cacheFile;

		public SongCache(string folder) {
			_folder = folder;

			string hex = BitConverter.ToString(SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(folder))).Replace("-", "");

			_cacheFile = Path.Combine(GameManager.PersistentDataPath, "caches", $"{hex}.bin");
		}

		public void WriteCache(List<SongEntry> songs, List<XboxSTFSFile> conFiles) {
			if (songs.Count == 0) {
				return;
			}

			if (!Directory.Exists(Path.Combine(GameManager.PersistentDataPath, "caches"))) {
				Directory.CreateDirectory(Path.Combine(GameManager.PersistentDataPath, "caches"));
			}

			using var writer = new NullStringBinaryWriter(File.Open(_cacheFile, FileMode.Create, FileAccess.ReadWrite));

			writer.Write(CACHE_VERSION);
			writer.Write(conFiles.Count);
			foreach(var con in conFiles) {
				writer.Write(con.Filename);
			}

			foreach (var song in songs) {
				try {
					song.WriteMetadataToCache(writer);
					song.WriteCacheEnding(writer);
				} catch (Exception e) {
					Debug.LogError($"Failed to write {song.Name} to cache: {e}");
				}
			}
		}

		public Tuple<List<SongEntry>, List<XboxSTFSFile>> ReadCache() {
			var songs = new List<SongEntry>();
			var conFiles = new List<XboxSTFSFile>();

			if (!File.Exists(_cacheFile)) {
				Debug.LogError("Cache file does not exist. Skipping");
				return new(songs, conFiles);
			}

			using var reader = new BinaryReader(File.Open(_cacheFile, FileMode.Open, FileAccess.Read));

			int version = reader.ReadInt32();

			if (version != CACHE_VERSION) {
				ToastManager.ToastWarning("Song Cache version is invalid. Rescan required.");
				throw new Exception("Song Cache version is invalid. Rescan required.");
			}

			int numCons = reader.ReadInt32();
			for (int i = 0; i < numCons; ++i) {
				XboxSTFSFile conFile = XboxSTFSFile.LoadCON(reader.ReadString());
				if (conFile != null)
					conFiles.Add(conFile);
			}

			while (reader.BaseStream.Position < reader.BaseStream.Length) {
				try {
					SongEntry song = (SongType) reader.ReadInt32() switch {
						SongType.SongIni => new IniSongEntry(reader, _folder),
						SongType.ExtractedRbCon => new ExtractedConSongEntry(reader, conFiles, _folder),
						SongType.RbCon => new ConSongEntry(reader, conFiles, _folder),
						_ => null
					};

					if (song == null)
						throw new Exception();
					song.ReadCacheEnding(reader);
					songs.Add(song);
				} catch (Exception e) {
					Debug.Log("Reader position: " + reader.BaseStream.Position);
					Debug.LogError("Failed to read song from cache");
					Debug.LogError(e.Message);
					Debug.LogError(e.StackTrace);
					if (e is not ConMissingException)
						throw new Exception("Song Cache is corrupted.");
				}
			}

			return new(songs, conFiles);
		}
	}
}