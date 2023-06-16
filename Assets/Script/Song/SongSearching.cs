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
	public class SongSearching {

		private readonly static SongSearching _instance = new SongSearching();

		public static SongSearching Instance {
			get
			{
				return _instance;
			}
		}

		private readonly static List<(string, string)> troublesomeCharacters = new List<(string, string)>{
			("Æ", "AE") // Tool - Ænema
		};

		public IEnumerable<SongEntry> Search(string value){
			var split = value.Split(';');
			IEnumerable<SongEntry> songsOut = Enumerable.Empty<SongEntry>();

			foreach (var arg in split) {
				// songsOut = SearchSongs(arg);
				songsOut = songsOut.Union(SearchSongs(arg));
			}

			return songsOut.OrderBy(OrderBy());
		}

		private IEnumerable<SongEntry> SearchSongs(string arg){
			if (arg.StartsWith("artist:")) {
				var artist = arg[7..];
				return SearchByArtist(artist);
			}

			if (arg.StartsWith("source:")) {
				var source = arg[7..];
				return SearchBySource(source);
			}

			if (arg.StartsWith("album:")) {
				var album = arg[6..];
				return SearchByAlbum(album);
			}

			if (arg.StartsWith("charter:")) {
				var charter = arg[8..];
				return SearchByCharter(charter);
			}

			if (arg.StartsWith("year:")) {
				var year = arg[5..];
				return SearchByYear(year);
			}

			if (arg.StartsWith("genre:")) {
				var genre = arg[6..];
				return SearchByGenre(genre);
			}

			if (arg.StartsWith("instrument:")) {
				var instrument = arg[11..];

				if(!string.IsNullOrEmpty(instrument) && instrument.Length > 1 && instrument.StartsWith("-")){
					return SearchByMissingInstrument(instrument.Substring(1));
				}
				return SearchByInstrument(instrument);
			}

			return SongContainer.Songs
					.Select(i => new { score = Search(arg, i), songInfo = i })
					.Where(i => i.score >= 0)
					.OrderBy(i => i.score)
					.Select(i => i.songInfo);
		}

		private IEnumerable<SongEntry> SearchByArtist(string artist){
			return SongContainer.Songs
				.Where(i => RemoveDiacriticsAndArticle(i.Artist) == RemoveDiacriticsAndArticle(artist));
		}

		private IEnumerable<SongEntry> SearchBySource(string source){
			return SongContainer.Songs
				.Where(i => i.Source?.ToLower() == source.ToLower());
		}
		
		private IEnumerable<SongEntry> SearchByAlbum(string album){
			return SongContainer.Songs
				.Where(i => RemoveDiacritics(i.Album) == RemoveDiacritics(album));
		}

		private IEnumerable<SongEntry> SearchByCharter(string charter){
			return SongContainer.Songs
				.Where(i => i.Charter?.ToLower() == charter.ToLower());
		}

		private IEnumerable<SongEntry> SearchByYear(string year){
			return SongContainer.Songs
				.Where(i => i.Year?.ToLower() == year.ToLower());
		}

		private IEnumerable<SongEntry> SearchByGenre(string genre){
			return SongContainer.Songs
				.Where(i => i.Genre?.ToLower() == genre.ToLower());
		}

		private IEnumerable<SongEntry> SearchByInstrument(string instrument){
			return instrument switch {
				"band" => SongContainer.Songs.Where(i => i.BandDifficulty >= 0),
				"vocals" => SongContainer.Songs.Where(i => i.VocalParts < 2),
				"harmVocals" => SongContainer.Songs.Where(i => i.VocalParts >= 2),
				_ => SongContainer.Songs.Where(i =>
					i.HasInstrument(InstrumentHelper.FromStringName(instrument))),
			};
		}

		private IEnumerable<SongEntry> SearchByMissingInstrument(string instrument){
			return instrument switch {
				"band" => SongContainer.Songs.Where(i => i.BandDifficulty < 0),
				"vocals" => SongContainer.Songs.Where(i => i.VocalParts <= 0),
				// string s when s.StartsWith("-") => SongContainer.Songs.Where(SongIsMissing(instrument)),
				_ => SongContainer.Songs.Where(SongIsMissing(instrument)),
			};
		}

		private Func<SongEntry, bool> SongIsMissing(string instrument){
			return s => !s.HasInstrument(InstrumentHelper.FromStringName(instrument));
		}

		public static string RemoveDiacritics(string text) {
			if (text == null) {
				return null;
			}

			foreach(var c in troublesomeCharacters){
				text = text.Replace(c.Item1, c.Item2);
			}

			var normalizedString = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);
			var stringBuilder = new StringBuilder(capacity: normalizedString.Length);

			for (int i = 0; i < normalizedString.Length; i++) {
				char c = normalizedString[i];
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
			var textWithoutDiacretics = RemoveDiacritics(text);
			return SongSorting.RemoveArticle(textWithoutDiacretics);
		}

		private int Search(string input, SongEntry songInfo) {
			string normalizedInput = RemoveDiacritics(input);

			// Get name index
			string name = songInfo.NameNoParenthesis;
			int nameIndex = RemoveDiacritics(name).IndexOf(normalizedInput);

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

		private Func<SongEntry, string> OrderBy(){
			return SongSorting.Instance.SortBy();
		}
	}
}