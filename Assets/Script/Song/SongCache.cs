using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace YARG.Song {
	public class SongCache {
		
		private const int CACHE_VERSION = 23_04_30;

		private readonly string _folder;
		private readonly string _cacheFile;
		
		public SongCache(string folder) {
			_folder = folder;
			string hex = BitConverter.ToString(SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(folder))).Replace("-", "");
			
			_cacheFile = Path.Combine(GameManager.PersistentDataPath, "caches", $"{hex}.bin");
		}

		public List<SongEntry> ReadCache() {
			throw new NotImplementedException();
		}

		public void WriteCache(List<SongEntry> songs) {
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

		private static void WriteSongEntry(BinaryWriter writer, SongEntry song) {
			Debug.Log($"Writing {song.Name} to cache");
			
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

			if (song is ConSongEntry conSong) {
				// Write con stuff
			} else if (song is IniSongEntry iniSong) {
				writer.Write(iniSong.Playlist);
				writer.Write(iniSong.SubPlaylist);
				writer.Write(iniSong.IsModChart);
				writer.Write(iniSong.HasLyrics);
			}
			
			writer.Write(song.Checksum);
			writer.Write(song.Location);
		}
	}
}