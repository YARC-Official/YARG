using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace YARG.Song {
	public class SortedSongList {
		/// <summary>
		/// The order of the sections.
		/// </summary>
		private readonly List<string> _sectionsOrder;

		/// <summary>
		/// The dictionary of sections.
		/// </summary>
		private readonly Dictionary<string, List<SongEntry>> _sections;

		public IReadOnlyList<string> SectionNames => _sectionsOrder;

		public SortedSongList() {
			_sectionsOrder = new();
			_sections = new();
		}

		public SortedSongList(SortedSongList other) {
			_sectionsOrder = new(other._sectionsOrder);

			// Make sure to copy the sub-lists
			_sections = other._sections.ToDictionary(i => i.Key,
				i => new List<SongEntry>(i.Value));
		}

		public void AddSongToSection(string section, SongEntry song) {
			if (_sections.TryGetValue(section, out var list)) {
				list.Add(song);
			} else {
				var newList = new List<SongEntry> {
					song
				};

				_sectionsOrder.Add(section);
				_sections.Add(section, newList);
			}
		}

		public IReadOnlyList<SongEntry> SongsInSection(string section) {
			if (_sections.TryGetValue(section, out var list)) {
				return list;
			}

			return null;
		}

		public int SongCount() {
			return _sections.Select(i => i.Value.Count).Sum();
		}

		public void Intersect(List<SongEntry> songs) {
			// Intersect
			var sectionsToRemove = new List<string>();
			foreach (var (section, list) in _sections) {
				foreach (var song in new List<SongEntry>(list)) {
					if (songs.Contains(song)) {
						continue;
					}

					list.Remove(song);
				}

				if (list.Count == 0) {
					sectionsToRemove.Add(section);
				}
			}

			// Remove empty sections
			foreach (var empty in sectionsToRemove) {
				_sectionsOrder.Remove(empty);
				_sections.Remove(empty);
			}
		}
	}

	public static class SongSorting {
		public enum Sort {
			Song = 0,
			Artist,
			Source,
			Year,
			Duration,
			Genre,
			Album,
			Charter,
		}

		private static readonly Func<SongEntry, string>[] HeaderFunctions = {
			song => GetFirstCharacter(song.NameNoParenthesis),                      // Song
			song => song.Artist,                                                    // Artist
			song => SongSources.SourceToGameName(song.Source),                      // Source
			song => GetDecade(song.Year),                                           // Year
			song => GetSongDurationBySection(song.SongLengthTimeSpan.TotalMinutes), // Duration
			song => song.Genre.ToUpper(),                                           // Genre
			song => GetFirstCharacter(song.Album),                                  // Album
			song => GetFirstCharacter(song.Charter),                                // Charter
		};

		private static readonly Func<SongEntry, string>[] OrderFunctions = {
			song => RemoveArticle(song.NameNoParenthesis),       // Song
			song => RemoveArticle(song.Artist),                  // Artist
			song => song.Source,                                 // Source
			song => GetYear(song.Year),                          // Year
			song => ((int) song.SongLengthTimeSpan.TotalSeconds) // Duration
				.ToString(CultureInfo.InvariantCulture),
			song => song.Genre,                                  // Genre
			song => song.Album,                                  // Album
			song => song.Charter,                                // Charter
		};

		private static readonly string[] Articles = {
			"The ", // The beatles, The day that never comes
			"El ",  // El final, El sol no regresa
			"La ",  // La quinta estacion, La bamba, La muralla verde
			"Le ",  // Le temps de la rentrée
			"Les ", // Les Rita Mitsouko, Les Wampas
			"Los ", // Los fabulosos cadillacs, Los enanitos verdes,
		};

		private static SortedSongList[] _sortCache;

		public static void GenerateSortCache() {
			_sortCache = new SortedSongList[HeaderFunctions.Length];

			var defaultOrderingFunction = OrderFunctions[0];

			for (int i = 0; i < HeaderFunctions.Length; i++) {
				var headerFunc = HeaderFunctions[i];
				var orderFunc = OrderFunctions[i];

				// Sort the songs
				var songList = new SortedSongList();
				var sortedSongs = SongContainer.Songs
					.OrderBy(song => orderFunc(song).ToUpperInvariant());

				// Then sort by name (if it isn't already)
				if (i != 0) {
					sortedSongs = sortedSongs
						.ThenBy(song => defaultOrderingFunction(song).ToUpperInvariant());
				}

				// Then separate them by sections
				foreach (var song in sortedSongs) {
					songList.AddSongToSection(headerFunc(song).ToUpperInvariant(), song);
				}

				_sortCache[i] = songList;
			}
		}

		public static SortedSongList GetSortCache(Sort sort) {
			return _sortCache[(int) sort];
		}

		public static string RemoveArticle(string name) {
			if (string.IsNullOrEmpty(name)) {
				return name;
			}

			foreach (var article in Articles) {
				if (name.StartsWith(article, StringComparison.InvariantCultureIgnoreCase)) {
					return name[article.Length..];
				}
			}

			return name;
		}

		private static string GetSongDurationBySection(double minutes) {
			return minutes switch {
				<= 0.00  => "-",
				<= 2.00  => "00:00 - 02:00",
				<= 5.00  => "02:00 - 05:00",
				<= 10.00 => "05:00 - 10:00",
				<= 15.00 => "10:00 - 15:00",
				<= 20.00 => "15:00 - 20:00",
				_        => "20:00+",
			};
		}

		private static string GetFirstCharacter(string value) {
			if (string.IsNullOrEmpty(value)) {
				return string.Empty;
			}

			var name = SongSearching.RemoveDiacriticsAndArticle(value);

			if (Regex.IsMatch(name, @"^\W")) {
				return string.Empty;
			}

			if (Regex.IsMatch(name, @"^\d")) {
				return "0-9";
			}

			return name[..1].ToUpper();
		}

		private static string GetYear(string value) {
			if (string.IsNullOrEmpty(value)){
				return string.Empty;
			}

			var match = Regex.Match(value, @"(\d{4})");
			if (string.IsNullOrEmpty(match.Value)) {
				return value;
			}

			return match.Value[..4];
		}

		private static string GetDecade(string value) {
			if (string.IsNullOrEmpty(value)) {
				return string.Empty;
			}

			var match = Regex.Match(value, @"(\d{4})");
			if (string.IsNullOrEmpty(match.Value)) {
				return value;
			}

			return match.Value[..3] + "0s";
		}

		public static string GetNextSortButtonName(Sort sortCriteria) {
			return sortCriteria switch {
				Sort.Song     => "Order by Artist",
				Sort.Artist   => "Order by Source",
				Sort.Source   => "Order by Year",
				Sort.Year     => "Order by Duration",
				Sort.Duration => "Order by Genre",
				Sort.Genre    => "Order by Album",
				Sort.Album    => "Order by Charter",
				Sort.Charter  => "Order by Song",
				_             => "Order by Song"
			};
		}

		public static Sort GetNextSortCriteria(Sort sortCriteria) {
			var next = (int) sortCriteria + 1;
			if (next >= Enum.GetNames(typeof(Sort)).Length) {
				next = 0;
			}

			return (Sort) next;
		}
	}
}