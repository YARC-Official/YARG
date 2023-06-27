using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using YARG.UI;
using XboxSTFS;
using YARG.Util;

namespace YARG.Song {
	public readonly struct CacheFolder {
		/// <summary>
		/// The date revision of the cache format.
		/// Format is YY_MM_DD_RR: Y = year, M = month, D = day, R = revision (reset across dates, only increment
		/// if multiple cache version changes happen in a single day).
		/// </summary>
		private const int CACHE_VERSION = 23_06_05_01;

		private readonly string _cacheFile;

		public readonly string Folder;
		public readonly bool Portable;

		public CacheFolder(string folder, bool portable) {
			Folder = folder;

			if (!portable) {
				string hex = BitConverter.ToString(SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(folder))).Replace("-", "");
				_cacheFile = Path.Combine(PathHelper.PersistentDataPath, "caches", $"{hex}.bin");
			} else {
				_cacheFile = Path.Combine(folder, "cache.bin");
			}

			Portable = portable;
		}

		public void WriteCache(List<SongEntry> songs, List<XboxSTFSFile> conFiles) {
			if (songs.Count == 0) {
				return;
			}

			if (!Directory.Exists(Path.Combine(PathHelper.PersistentDataPath, "caches"))) {
				Directory.CreateDirectory(Path.Combine(PathHelper.PersistentDataPath, "caches"));
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
						SongType.SongIni => new IniSongEntry(reader, Folder),
						SongType.ExtractedRbCon => new ExtractedConSongEntry(reader, conFiles, Folder),
						SongType.RbCon => new ConSongEntry(reader, conFiles, Folder),
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

		public override int GetHashCode() {
			return HashCode.Combine(Folder, Portable);
		}

		public bool Equals(CacheFolder other) {
			return Folder == other.Folder && Portable == other.Portable;
		}

		public override bool Equals(object obj) {
			return obj is CacheFolder other && Equals(other);
		}

		public static bool operator ==(CacheFolder left, CacheFolder right) {
			return left.Equals(right);
		}

		public static bool operator !=(CacheFolder left, CacheFolder right) {
			return !left.Equals(right);
		}

		public override string ToString() {
			return Folder;
		}
	}
}