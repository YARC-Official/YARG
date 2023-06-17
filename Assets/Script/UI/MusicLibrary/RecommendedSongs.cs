using System;
using System.Text;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using YARG.Audio;
using YARG.Data;
using YARG.Input;
using YARG.Song;
using YARG.UI.MusicLibrary.ViewTypes;
using Random = UnityEngine.Random;
using System.Threading;
using YARG.Settings;

namespace YARG.UI.MusicLibrary {
	public class RecommendedSongs {

		private readonly static RecommendedSongs _instance = new RecommendedSongs();

		private static readonly int TRIES = 10;

		public static RecommendedSongs Instance {
			get
			{
				return _instance;
			}
		}

		private List<SongEntry> _recommendedSongs;

		public List<SongEntry> GetRecommendedSongs() {
			_recommendedSongs = new();

			AddMostPlayedSongs();
			AddRandomSong();
			return GetReversedRecommendedSongs();
		}

		private void AddMostPlayedSongs() {
			var mostPlayed = GetMostPlayedSongs();

			if(mostPlayed.Count <= 0) {
				return;
			}

			AddTwoTopTenMostPlayedSongs(mostPlayed);
			AddTwoRandomSongsMostPlayedArtists(mostPlayed);
		}

		private List<SongEntry> GetMostPlayedSongs() {
			return ScoreManager.SongsByPlayCount().Take(10).ToList();
		}

		private void AddTwoTopTenMostPlayedSongs(List<SongEntry> songs) {
			var count = songs.Count;

			// Add two random top ten most played songs (ten tries each)
			for (int i = 0; i < 2; i++) {
				for (int t = 0; t < TRIES; t++) {

					int n = Random.Range(0, count);
					var song = songs[n];

					if (_recommendedSongs.Contains(song)) {
						continue;
					}

					_recommendedSongs.Add(song);
					break;
				}
			}
		}

		private void AddTwoRandomSongsMostPlayedArtists(List<SongEntry> songs) {
			// Add two random songs from artists that are in the most played (ten tries each)
			for (int i = 0; i < 2; i++) {
				for (int t = 0; t < TRIES; t++) {
					var sameArtistSongs = GetAllSongsFromSameArtist(songs);

					if (sameArtistSongs.Count <= 1) {
						continue;
					}

					// Pick
					var count = sameArtistSongs.Count;
					var n = Random.Range(0, count);
					var song = sameArtistSongs[n];

					// Skip if included in most played songs
					if (songs.Contains(song)) {
						continue;
					}

					// Skip if already included in recommendedSongs
					if (_recommendedSongs.Contains(song)) {
						continue;
					}

					// Add
					_recommendedSongs.Add(song);
					break;
				}
			}
		}

		private List<SongEntry> GetAllSongsFromSameArtist(List<SongEntry> songs){
			var count = songs.Count;
			int n = Random.Range(0, count);
			var baseSong = songs[n];
			var artist = baseSong.Artist;

			return SongContainer.Songs
				.Where(i => RemoveDiacriticsAndArticle(i.Artist) == RemoveDiacriticsAndArticle(artist))
				.ToList();
		}

		private string RemoveDiacriticsAndArticle(string value){
			return SongSearching.RemoveDiacriticsAndArticle(value);
		}

		private void AddRandomSong() {
			var songs = SongContainer.Songs;
			var count = songs.Count;

			// Add a completely random song (ten tries)
			for (int t = 0; t < TRIES; t++) {
				int n = Random.Range(0, count);

				var song = songs[n];

				if (_recommendedSongs.Contains(song)) {
					continue;
				}

				_recommendedSongs.Add(song);
				break;
			}
		}

		private List<SongEntry> GetReversedRecommendedSongs(){
			// Reverse list because we add it backwards
			_recommendedSongs.Reverse();
			return _recommendedSongs;
		}
	}
}