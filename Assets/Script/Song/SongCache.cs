using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace YARG.Song {
	public class SongCache {
		
		private const int CACHE_VERSION = 23_05_01;

		private readonly string _cacheFile;
		
		public SongCache(string folder) {
			string hex = BitConverter.ToString(SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(folder))).Replace("-", "");
			
			_cacheFile = Path.Combine(GameManager.PersistentDataPath, "caches", $"{hex}.bin");
		}

		public void WriteCache(List<SongEntry> songs) {
			if (songs.Count == 0) {
				return;
			}
			
			using var writer = new BinaryWriter(File.Open(_cacheFile, FileMode.Create, FileAccess.ReadWrite));
			
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
				throw new Exception("Song Cache version is invalid. Rescan required.");
			}

			while (reader.BaseStream.Position < reader.BaseStream.Length) {
				var song = ReadSongEntry(reader);
				if (song is not null) {
					songs.Add(song);
				} else {
					throw new Exception("Song Cache is corrupted.");
				}
			}

			return songs;
		}
		
		private static void WriteSongEntry(BinaryWriter writer, SongEntry song) {
			Debug.Log($"Writing {song.Name} to cache");

			if (song is IniSongEntry) {
				writer.Write((int)SongType.SongIni);
			} else if (song is RawConSongEntry) {
				writer.Write((int)SongType.RbConRaw);
			}
			// Unextracted con
			
			writer.Write(song.Name);
			writer.Write(song.Artist);
			writer.Write(song.Charter);
			writer.Write(song.Album);
			writer.Write(song.AlbumTrack);
			writer.Write(song.PlaylistTrack);
			writer.Write(song.Genre);
			writer.Write(song.Year);
			writer.Write(song.SongLength);
			writer.Write(song.PreviewStart);
			writer.Write(song.PreviewEnd);
			writer.Write(song.LoadingPhrase);
			writer.Write(song.HopoThreshold);
			writer.Write(song.EighthNoteHopo);
			writer.Write(song.MultiplierNote);
			writer.Write(song.Icon);

			switch (song)
			{
				case RawConSongEntry conSong:
					// Write con stuff
					break;
				case IniSongEntry iniSong:
					writer.Write(iniSong.Playlist);
					writer.Write(iniSong.SubPlaylist);
					writer.Write(iniSong.IsModChart);
					writer.Write(iniSong.HasLyrics);
					break;
			}
			
			writer.Write(song.Checksum);
			writer.Write(song.NotesFile);
			writer.Write(song.Location);
		}

		private static SongEntry ReadSongEntry(BinaryReader reader) {
			try {
				SongEntry result = null;
				var type = (SongType)reader.ReadInt32();

				result = type switch {
					SongType.RbConRaw => new RawConSongEntry(),
					SongType.SongIni  => new IniSongEntry(),
					_                 => result
				};

				result.Type = type;
				
				result.Name = reader.ReadString();
				result.Artist = reader.ReadString();
				result.Charter = reader.ReadString();
				result.Album = reader.ReadString();
				result.AlbumTrack = reader.ReadInt32();
				result.PlaylistTrack = reader.ReadInt32();
				result.Genre = reader.ReadString();
				result.Year = reader.ReadString();
				result.SongLength = reader.ReadInt32();
				result.PreviewStart = reader.ReadInt32();
				result.PreviewEnd = reader.ReadInt32();
				result.LoadingPhrase = reader.ReadString();
				result.HopoThreshold = reader.ReadInt32();
				result.EighthNoteHopo = reader.ReadBoolean();
				result.MultiplierNote = reader.ReadInt32();
				result.Icon = reader.ReadString();

				switch (type)
				{
					case SongType.RbConRaw:
						// Con specific properties
						break;
					case SongType.SongIni:
					{
						// Ini specific properties
						var iniSong = (IniSongEntry)result;
					
						iniSong.Playlist = reader.ReadString();
						iniSong.SubPlaylist = reader.ReadString();
						iniSong.IsModChart = reader.ReadBoolean();
						iniSong.HasLyrics = reader.ReadBoolean();
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