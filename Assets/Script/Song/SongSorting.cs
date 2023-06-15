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
	public class SongSorting {
		public enum SortCriteria {
			SONG = 1,
			ARTIST,
			SOURCE,
			YEAR,
			DURATION,
			GENRE,
			ALBUM,
			CHARTER,
		}
		public enum PreviousNext {
			PREVIOUS = 1,
			NEXT,
		}

		private readonly static List<string> articles = new List<string>{
			"The ",// The beatles, The day that never comes
			"El ", // El final, El sol no regresa
			"La ", // La quinta estacion, La bamba, La muralla verde
			"Le ", // Le temps de la rentrée
			"Les ", // Les Rita Mitsouko, Les Wampas
			"Los ", // Los fabulosos cadillacs, Los enanitos verdes,
		};

		private readonly static SongSorting _instance = new SongSorting();

		private readonly static string EMPTY_VALUE = " ";

		private Func<SongViewType, string> index;

		private Func<SongEntry, string> sortBy;

		private List<string> songsFirstLetter;

		public static SongSorting Instance {
			get
			{
				return _instance;
			}
		}

		private SongSorting(){
			OrderByName();
		}

		public void OrderBy(SortCriteria sortCriteria) {
			_ = sortCriteria switch {
				SortCriteria.SONG => OrderByName(),
				SortCriteria.ARTIST => OrderByArtist(),
				SortCriteria.SOURCE => OrderBySource(),
				SortCriteria.YEAR => OrderByYear(),
				SortCriteria.DURATION => OrderByDuration(),
				SortCriteria.GENRE => OrderByGenre(),
				SortCriteria.ALBUM => OrderByAlbum(),
				SortCriteria.CHARTER => OrderByCharter(),
				_ => OrderByName()
			};
		}

		private bool OrderByName() {
			index = song => {
				string name = song.SongEntry.NameNoParenthesis;
				return GetFirstCharacter(name);
			};

			sortBy = song => {
				string name = song.NameNoParenthesis.ToUpper();
				return SongSearching.RemoveDiacriticsAndArticle(name);
			};

			return true;
		}

		public static string RemoveArticle(string name){
			if(string.IsNullOrEmpty(name)){
				return name;
			}

			foreach (var article in articles) {
				if(name.StartsWith(article, StringComparison.InvariantCultureIgnoreCase)){
					return name.Substring(article.Length);
				}
			}
			return name;
		}

		private bool OrderByArtist() {
			index = song => {
				string artist = song.SongEntry.Artist;
				return GetFirstCharacter(artist);
			};

			sortBy = song => {
				string artist = song.Artist.ToUpper();
				return RemoveArticle(artist);
			};

			return true;
		}

		private bool OrderByDuration() {
			index = song => {
				return GetSongLengthBySection(song.SongEntry);
			};

			sortBy = song => {
				return song.SongLengthTimeSpan.ToString(@"h\:mm\:ss");
			};

			return true;
		}

		private string GetSongLengthBySection(SongEntry songEntry){
			var minutes = songEntry.SongLengthTimeSpan.TotalMinutes;

			return minutes switch {
				<= 0.00f => "-",
				<= 2.00f => "00:00 - 02:00",
				<= 5.00f => "02:00 - 05:00",
				<= 10.00f => "05:00 - 10:00",
				<= 15.00f => "10:00 - 15:00",
				<= 20.00f => "15:00 - 20:00",
				_ => "20:00+",
			};
		}

		private bool OrderBySource() {
			index = song => {
				string source = song.SongEntry.Source;
				return SongSources.SourceToGameName(source);
			};

			sortBy = song => {
				string source = song.Source;
				return SongSources.SourceToGameName(source);
			};

			return true;
		}

		private bool OrderByAlbum() {
			index = song => {
				string album = song.SongEntry.Album;
				return GetFirstCharacter(album);
			};

			sortBy = song => {
				string album = song.Album.ToUpper();

				if(string.IsNullOrEmpty(album)){
					return EMPTY_VALUE;
				}

				return album.ToUpper();

			};

			return true;
		}

		private bool OrderByCharter() {
			index = song => {
				string charter = song.SongEntry.Charter;
				return GetFirstCharacter(charter);
			};

			sortBy = song => {
				string charter = song.Charter.ToUpper();

				if(string.IsNullOrEmpty(charter)){
					return EMPTY_VALUE;
				}

				return charter.ToUpper();
			};

			return true;
		}

		private bool OrderByGenre() {
			index = song => {
				return song.SongEntry.Genre.ToUpper();
			};

			sortBy = song => {
				return song.Genre.ToUpper();
			};

			return true;
		}
		
		private bool OrderByYear() {
			index = song => {
				string year = song.SongEntry.Year;
				return GetDecade(year);
			};

			sortBy = song => {
				string year = song.Year;
				return GetYear(year);
			};

			return true;
		}

		private static string GetFirstCharacter(string value) {
			if(string.IsNullOrEmpty(value)){
				return EMPTY_VALUE;
			}

			var name = SongSearching.RemoveDiacriticsAndArticle(value);

			if(Regex.IsMatch(name, @"^\W")){
				return EMPTY_VALUE;
			}

			if(Regex.IsMatch(name, @"^\d")){
				return "0-9";
			}
			return name.Substring(0,1).ToUpper();
		}

		private static string GetYear(string value) {
			if(string.IsNullOrEmpty(value)){
				return EMPTY_VALUE;
			}
			var match = Regex.Match(value, @"(\d{4})");
			if(string.IsNullOrEmpty(match.Value)){
				return value;
			}
			return match.Value.Substring(0,4);
		}

		private static string GetDecade(string value) {
			if(string.IsNullOrEmpty(value)){
				return EMPTY_VALUE;
			}
			var match = Regex.Match(value, @"(\d{4})");
			if(string.IsNullOrEmpty(match.Value)){
				return value;
			}
			return match.Value.Substring(0,3) + "0s";
		}

		public void UpdateIndex(List<ViewType> songs){
			songsFirstLetter = songs
				.OfType<SongViewType>()
				.Select(index)
				.Where(value => !string.IsNullOrEmpty(value))
				.Distinct()
				.OrderBy(ch => ch)
				.ToList();
		}

		public List<string> GetSongsFirstLetter(){
			return songsFirstLetter;
		}

		public int GetSectionsSize(){
			return songsFirstLetter.Count;
		}

		public string GetLastSection(){
			return songsFirstLetter[songsFirstLetter.Count - 1];
		}

		public Func<SongEntry, string> SortBy(){
			return sortBy;
		}

		public Func<SongViewType, string> Index(){
			return index;
		}

		public SortCriteria GetNextSortCriteria(SortCriteria sortCriteria) {
			return sortCriteria switch {
				SortCriteria.SONG => SortCriteria.ARTIST,
				SortCriteria.ARTIST => SortCriteria.SOURCE,
				SortCriteria.SOURCE => SortCriteria.YEAR,
				SortCriteria.YEAR => SortCriteria.DURATION,
				SortCriteria.DURATION => SortCriteria.GENRE,
				SortCriteria.GENRE => SortCriteria.ALBUM,
				SortCriteria.ALBUM => SortCriteria.CHARTER,
				SortCriteria.CHARTER => SortCriteria.SONG,
				_ => SortCriteria.SONG
			};
		}

		public string GetNextSortCriteriaButtonName(SortCriteria sortCriteria) {
			return sortCriteria switch {
				SortCriteria.SONG => "Order by Artist",
				SortCriteria.ARTIST => "Order by Source",
				SortCriteria.SOURCE => "Order by Year",
				SortCriteria.YEAR => "Order by Duration",
				SortCriteria.DURATION =>"Order by Genre",
				SortCriteria.GENRE => "Order by Album",
				SortCriteria.ALBUM => "Order by Charter",
				SortCriteria.CHARTER => "Order by Song",
				_ => "Order by Song"
			};
		}

		private string GetNewSectionLetterOrNumber(SongViewType song, PreviousNext order = PreviousNext.NEXT){
			if(null == song){
				return null;
			}

			string firstCharacter = index(song);

			if(string.IsNullOrEmpty(firstCharacter)){
				return null;
			}

			int indexOfActualLetter = songsFirstLetter.FindIndex(letter => {
				return String.Equals(letter, firstCharacter, StringComparison.OrdinalIgnoreCase);
			});

			var newCharacter = firstCharacter;
			if (order == PreviousNext.NEXT) {
				if (indexOfActualLetter == (songsFirstLetter.Count - 1)) {
					newCharacter = songsFirstLetter[0];
				} else {
					newCharacter = songsFirstLetter[indexOfActualLetter + 1];
				}
			} else {
				if (indexOfActualLetter == 0) {
					newCharacter = songsFirstLetter[songsFirstLetter.Count - 1];
				} else {
					newCharacter = songsFirstLetter[indexOfActualLetter - 1];
				}
			}
			return newCharacter;
		}

		public int SelectNewSection(List<ViewType> songs, int selectedIndex, SongViewType song, int skip, PreviousNext order){

			string newCharacter = GetNewSectionLetterOrNumber(song, order);

			// If an error occurs no change is made
			if (string.IsNullOrEmpty(newCharacter)) {
				return selectedIndex;
			}

			var _index = songs.FindIndex(skip, song =>
				song is SongViewType songType &&
					String.Equals(
						index(songType),
						newCharacter,
						StringComparison.OrdinalIgnoreCase)
				);

			return _index;
		}

		public int GetIndexOfLetter(List<ViewType> songs, string value, int skip){
			return songs.FindIndex(skip, song =>
				song is SongViewType songType &&
					String.Equals(
						index(songType),
						value,
						StringComparison.OrdinalIgnoreCase
					)
			);
		}
	}
}