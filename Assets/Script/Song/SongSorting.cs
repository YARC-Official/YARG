using System;
using System.Collections;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

namespace YARG.Song {
	public class SortedSongList {
		private readonly OrderedDictionary _sections;

		public SortedSongList() {
			_sections = new();
		}

		public SortedSongList(SortedSongList other) {
			_sections = new();

			// OrderedDictionary sucks...
			foreach (DictionaryEntry entry in other._sections) {
				_sections.Add(entry.Key, entry.Value);
			}
		}

		public void AddSongToSection(string section, SongEntry song) {
			if (!_sections.Contains(section)) {
				var list = new List<SongEntry> {
					song
				};

				_sections.Add(section, list);
			} else {
				((List<SongEntry>) _sections[section]).Add(song);
			}
		}

		public IReadOnlyList<SongEntry> SongsInSection(string sectionName) {
			foreach (DictionaryEntry section in _sections) {
				if ((string) section.Key != sectionName) {
					continue;
				}

				return (List<SongEntry>) section.Value;
			}

			return null;
		}

		public void Intersect(List<SongEntry> songs) {
			// Intersect
			var removeSections = new HashSet<string>();
			foreach (DictionaryEntry section in _sections) {
				var list = (List<SongEntry>) section.Value;

				foreach (var song in new List<SongEntry>(list)) {
					if (songs.Contains(song)) {
						continue;
					}

					list.Remove(song);
				}

				if (list.Count == 0) {
					removeSections.Add((string) section.Key);
				}
			}

			// Remove empty sections
			foreach (var empty in removeSections) {
				_sections.Remove(empty);
			}
		}

		public ICollection SectionsCollection() {
			return _sections.Keys;
		}
	}

	public static class SongSorting {
		public enum PreviousOrNext {
			Next = 0,
			Previous,
		}

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

		private static readonly Func<SongEntry, string>[] OrderingFunctions = {
			song => GetFirstCharacter(song.NameNoParenthesis),                      // Song
			song => GetFirstCharacter(song.Artist),                                 // Artist
			song => SongSources.SourceToGameName(song.Source),                      // Source
			song => GetDecade(song.Year),                                           // Year
			song => GetSongDurationBySection(song.SongLengthTimeSpan.TotalMinutes), // Duration
			song => song.Genre.ToUpper(),                                           // Genre
			song => GetFirstCharacter(song.Album),                                  // Album
			song => GetFirstCharacter(song.Charter),                                // Charter
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
			_sortCache = new SortedSongList[OrderingFunctions.Length];

			for (int i = 0; i < OrderingFunctions.Length; i++) {
				var func = OrderingFunctions[i];

				var songList = new SortedSongList();
				var sortedSongs = SongContainer.Songs.ToList().OrderBy(song => func(song));

				foreach (var song in sortedSongs) {
					songList.AddSongToSection(func(song), song);
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
				Sort.Duration =>"Order by Genre",
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