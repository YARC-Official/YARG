using System;
using System.Text;
using System.Text.RegularExpressions;
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

namespace YARG.Song {
	public static class SongSearching {
		private static readonly List<(string, string)> SearchLeniency = new() {
			("Æ", "AE") // Tool - Ænema
		};

		// TODO: Make search query separate. This gets rid of the need of a tuple in SearchSongs
		public static SortedSongList Search(string value, SongSorting.Sort sort) {
			var songsOut = new List<SongEntry>(SongContainer.Songs);
			bool searching = false;

			if (!string.IsNullOrEmpty(value)) {
				var split = value.Split(';');
				foreach (var arg in split) {
					var (isSearching, songsEnumerable) = SearchSongs(arg);
					var songs = songsEnumerable.ToList();

					if (isSearching) {
						searching = true;
						songsOut.Clear();
						songsOut.AddRange(songs);
					} else {
						foreach (var song in songsOut.ToArray()) {
							if (songs.Contains(song)) {
								continue;
							}

							songsOut.Remove(song);
						}
					}
				}
			}

			SortedSongList sortedSongs;
			if (searching) {
				sortedSongs = new SortedSongList();
				foreach (var song in songsOut) {
					sortedSongs.AddSongToSection("Search Results", song);
				}
			} else {
				sortedSongs = new SortedSongList(SongSorting.GetSortCache(sort));
				sortedSongs.Intersect(songsOut);
			}

			return sortedSongs;
		}

		private static (bool isSearching, IEnumerable<SongEntry>) SearchSongs(string arg){
			if (arg.StartsWith("artist:")) {
				var artist = arg[7..];
				return (false, SearchByArtist(artist));
			}

			if (arg.StartsWith("source:")) {
				var source = arg[7..];
				return (false, SearchBySource(source));
			}

			if (arg.StartsWith("album:")) {
				var album = arg[6..];
				return (false, SearchByAlbum(album));
			}

			if (arg.StartsWith("charter:")) {
				var charter = arg[8..];
				return (false, SearchByCharter(charter));
			}

			if (arg.StartsWith("year:")) {
				var year = arg[5..];
				return (false, SearchByYear(year));
			}

			if (arg.StartsWith("genre:")) {
				var genre = arg[6..];
				return (false, SearchByGenre(genre));
			}

			if (arg.StartsWith("instrument:")) {
				var instrument = arg[11..];

				if(!string.IsNullOrEmpty(instrument) && instrument.Length > 1 && instrument.StartsWith("-")){
					return (false, SearchByMissingInstrument(instrument.Substring(1)));
				}
				return (false, SearchByInstrument(instrument));
			}

			return (true, SongContainer.Songs
					.Select(i => new { score = Search(arg, i), songInfo = i })
					.Where(i => i.score >= 0)
					.OrderBy(i => i.score)
					.Select(i => i.songInfo));
		}

		private static IEnumerable<SongEntry> SearchByArtist(string artist){
			return SongContainer.Songs
				.Where(i => RemoveDiacriticsAndArticle(i.Artist) == RemoveDiacriticsAndArticle(artist));
		}

		private static IEnumerable<SongEntry> SearchBySource(string source){
			return SongContainer.Songs
				.Where(i => i.Source?.ToLower() == source.ToLower());
		}

		private static IEnumerable<SongEntry> SearchByAlbum(string album){
			return SongContainer.Songs
				.Where(i => RemoveDiacritics(i.Album) == RemoveDiacritics(album));
		}

		private static IEnumerable<SongEntry> SearchByCharter(string charter){
			return SongContainer.Songs
				.Where(i => i.Charter?.ToLower() == charter.ToLower());
		}

		private static IEnumerable<SongEntry> SearchByYear(string year){
			return SongContainer.Songs
				.Where(i => i.Year?.ToLower() == year.ToLower());
		}

		private static IEnumerable<SongEntry> SearchByGenre(string genre){
			return SongContainer.Songs
				.Where(i => i.Genre?.ToLower() == genre.ToLower());
		}

		private static IEnumerable<SongEntry> SearchByInstrument(string instrument){
			return instrument switch {
				"band" => SongContainer.Songs.Where(i => i.BandDifficulty >= 0),
				"vocals" => SongContainer.Songs.Where(i => i.VocalParts < 2),
				"harmVocals" => SongContainer.Songs.Where(i => i.VocalParts >= 2),
				_ => SongContainer.Songs.Where(i =>
					i.HasInstrument(InstrumentHelper.FromStringName(instrument))),
			};
		}

		private static IEnumerable<SongEntry> SearchByMissingInstrument(string instrument){
			return instrument switch {
				"band" => SongContainer.Songs.Where(i => i.BandDifficulty < 0),
				"vocals" => SongContainer.Songs.Where(i => i.VocalParts <= 0),
				// string s when s.StartsWith("-") => SongContainer.Songs.Where(SongIsMissing(instrument)),
				_ => SongContainer.Songs.Where(SongIsMissing(instrument)),
			};
		}

		private static Func<SongEntry, bool> SongIsMissing(string instrument){
			return s => !s.HasInstrument(InstrumentHelper.FromStringName(instrument));
		}

		public static string RemoveDiacritics(string text) {
			if (text == null) {
				return null;
			}

			foreach (var c in SearchLeniency) {
				text = text.Replace(c.Item1, c.Item2);
			}

			var normalizedString = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);
			var stringBuilder = new StringBuilder(capacity: normalizedString.Length);

			foreach (char c in normalizedString) {
				var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
				if (unicodeCategory != UnicodeCategory.NonSpacingMark) {
					stringBuilder.Append(c);
				}
			}

			return stringBuilder
				.ToString()
				.Normalize(NormalizationForm.FormC);
		}

		public static string RemoveDiacriticsAndArticle(string text){
			var textWithoutDiacritics = RemoveDiacritics(text);
			return SongSorting.RemoveArticle(textWithoutDiacritics);
		}

		private static int Search(string input, SongEntry songInfo) {
			string normalizedInput = RemoveDiacritics(input);

			// Get name index
			string name = songInfo.NameNoParenthesis;
			int nameIndex = RemoveDiacritics(name).IndexOf(normalizedInput, StringComparison.Ordinal);

			// Get artist index
			string artist = songInfo.Artist;
			int artistIndex = RemoveDiacritics(artist).IndexOf(normalizedInput, StringComparison.Ordinal);

			// Return the best search
			if (nameIndex == -1 && artistIndex == -1) {
				return -1;
			}

			if (nameIndex == -1) {
				return artistIndex;
			}

			if (artistIndex == -1) {
				return nameIndex;
			}
			return Mathf.Min(nameIndex, artistIndex);
		}
	}
}