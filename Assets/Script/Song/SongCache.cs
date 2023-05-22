using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using YARG.Data;

namespace YARG.Song {
	public class SongCache {

		/// <summary>
		/// The date in which the cache version is based on (and cache revision)
		/// </summary>
		private const int CACHE_VERSION = 23_05_21_01;

		private readonly string _folder;
		private readonly string _cacheFile;

		public SongCache(string folder) {
			_folder = folder;

			string hex = BitConverter.ToString(SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(folder))).Replace("-", "");

			_cacheFile = Path.Combine(GameManager.PersistentDataPath, "caches", $"{hex}.bin");
		}

		public void WriteCache(List<SongEntry> songs) {
			if (songs.Count == 0) {
				return;
			}

			if (!Directory.Exists(Path.Combine(GameManager.PersistentDataPath, "caches"))) {
				Directory.CreateDirectory(Path.Combine(GameManager.PersistentDataPath, "caches"));
			}

			using var writer = new NullStringBinaryWriter(File.Open(_cacheFile, FileMode.Create, FileAccess.ReadWrite));

			writer.Write(CACHE_VERSION);

			foreach (var song in songs) {
				try {
					WriteSongEntry(writer, song);
				} catch (Exception e) {
					Debug.LogError($"Failed to write {song.Name} to cache: {e}");
				}
			}
		}

		public List<SongEntry> ReadCache() {
			var songs = new List<SongEntry>();

			if (!File.Exists(_cacheFile)) {
				Debug.LogError("Cache file does not exist. Skipping");
				return songs;
			}

			using var reader = new BinaryReader(File.Open(_cacheFile, FileMode.Open, FileAccess.Read));

			int version = reader.ReadInt32();

			if (version != CACHE_VERSION) {
				ToastManager.ToastWarning("Song Cache version is invalid. Rescan required.");
				throw new Exception("Song Cache version is invalid. Rescan required.");
			}

			while (reader.BaseStream.Position < reader.BaseStream.Length) {
				var song = ReadSongEntry(reader);
				if (song is not null) {
					song.CacheRoot = _folder;
					songs.Add(song);
				} else {
					throw new Exception("Song Cache is corrupted.");
				}
			}

			return songs;
		}

		private static void WriteSongEntry(BinaryWriter writer, SongEntry song) {
			// Debug.Log($"Writing {song.Name} to cache");

			bool isCON = false;
			if (song is IniSongEntry) {
				writer.Write((int) SongType.SongIni);
			} else {
				if (song is ConSongEntry conEntry) {
					if (conEntry.FLMidi == null) { // use the midi file offsets array to determine if CON or ExCON
						writer.Write((int) SongType.ExtractedRbCon);
					} else {
						writer.Write((int) SongType.RbCon);
						isCON = true;
					}
				}
			}

			writer.Write((int) song.DrumType);

			writer.Write(song.Name);
			writer.Write(song.Artist);
			writer.Write(song.Charter);
			writer.Write(song.IsMaster);
			writer.Write(song.Album);
			writer.Write(song.AlbumTrack);
			writer.Write(song.PlaylistTrack);
			writer.Write(song.Genre);
			writer.Write(song.Year);
			writer.Write(song.SongLength);
			writer.Write(song.PreviewStart);
			writer.Write(song.PreviewEnd);
			writer.Write(song.Delay);
			writer.Write(song.LoadingPhrase);
			writer.Write(song.HopoThreshold);
			writer.Write(song.EighthNoteHopo);
			writer.Write(song.MultiplierNote);
			writer.Write(song.Source);

			// Write difficulties
			writer.Write(song.PartDifficulties.Count);
			foreach (var difficulty in song.PartDifficulties) {
				writer.Write((int) difficulty.Key);
				writer.Write(difficulty.Value);
			}

			writer.Write(song.BandDifficulty);
			writer.Write(song.AvailableParts);
			writer.Write(song.VocalParts);

			if (song is IniSongEntry iniSong) {
				// These are CH specific ini properties
				writer.Write(iniSong.Playlist);
				writer.Write(iniSong.SubPlaylist);
				writer.Write(iniSong.IsModChart);
				writer.Write(iniSong.HasLyrics);
				writer.Write(iniSong.VideoStartOffset);
			} else {
				if (!isCON) {
					// Write ex-con stuff
					CacheHelpers.WriteExtractedConData(writer, (ExtractedConSongEntry) song);
				} else {
					// Write con stuff
					CacheHelpers.WriteExtractedConData(writer, (ConSongEntry) song);
					// Write con-exclusive stuff
					CacheHelpers.WriteConData(writer, (ConSongEntry) song);
				}
			}

			writer.Write(song.Checksum);
			writer.Write(song.NotesFile);
			writer.Write(song.Location);
		}

		private static SongEntry ReadSongEntry(BinaryReader reader) {
			try {
				SongEntry result = null;
				var type = (SongType) reader.ReadInt32();

				result = type switch {
					SongType.RbCon => new ConSongEntry(),
					SongType.ExtractedRbCon => new ExtractedConSongEntry(),
					SongType.SongIni => new IniSongEntry(),
					_ => throw new Exception("Unknown song type!")
				};

				result.SongType = type;

				result.DrumType = (DrumType) reader.ReadInt32();

				result.Name = reader.ReadString();
				result.Artist = reader.ReadString();
				result.Charter = reader.ReadString();
				result.IsMaster = reader.ReadBoolean();
				result.Album = reader.ReadString();
				result.AlbumTrack = reader.ReadInt32();
				result.PlaylistTrack = reader.ReadInt32();
				result.Genre = reader.ReadString();
				result.Year = reader.ReadString();
				result.SongLength = reader.ReadInt32();
				result.PreviewStart = reader.ReadInt32();
				result.PreviewEnd = reader.ReadInt32();
				result.Delay = reader.ReadDouble();
				result.LoadingPhrase = reader.ReadString();
				result.HopoThreshold = reader.ReadInt32();
				result.EighthNoteHopo = reader.ReadBoolean();
				result.MultiplierNote = reader.ReadInt32();
				result.Source = reader.ReadString();

				// Read difficulties
				int difficultyCount = reader.ReadInt32();
				for (var i = 0; i < difficultyCount; i++) {
					var part = (Instrument) reader.ReadInt32();
					int difficulty = reader.ReadInt32();
					result.PartDifficulties.Add(part, difficulty);
				}

				result.BandDifficulty = reader.ReadInt32();
				result.AvailableParts = (ulong) reader.ReadInt64();
				result.VocalParts = reader.ReadInt32();

				switch (type) {
					case SongType.ExtractedRbCon:
						CacheHelpers.ReadExtractedConData(reader, (ExtractedConSongEntry) result);
						break;
					case SongType.RbCon:
						CacheHelpers.ReadExtractedConData(reader, (ConSongEntry) result);
						CacheHelpers.ReadConData(reader, (ConSongEntry) result);
						break;
					case SongType.SongIni: {
							// Ini specific properties
							var iniSong = (IniSongEntry)result;

							iniSong.Playlist = reader.ReadString();
							iniSong.SubPlaylist = reader.ReadString();
							iniSong.IsModChart = reader.ReadBoolean();
							iniSong.HasLyrics = reader.ReadBoolean();
							iniSong.VideoStartOffset = reader.ReadInt32();
							break;
						}
				}

				result.Checksum = reader.ReadString();
				result.NotesFile = reader.ReadString();
				result.Location = reader.ReadString();

				return result;
			} catch (Exception e) {
				Debug.Log("Reader position: " + reader.BaseStream.Position);
				Debug.LogError("Failed to read song from cache");
				Debug.LogError(e.Message);
				Debug.LogError(e.StackTrace);
				return null;
			}
		}
	}
}