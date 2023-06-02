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

namespace YARG.Song {
	public class SongSorting {
		public enum SortCriteria {
			SONG = 1,
			ARTIST,
			SOURCE,
			YEAR,
			GENRE,
			ALBUM,
			CHARTER,
		}

		private readonly static SongSorting _instance = new SongSorting();

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
			index = song => {
				string name = song.SongEntry.NameNoParenthesis;
				return GetFirstCharacter(name);
			};

			sortBy = song => {
				return song.NameNoParenthesis;
			};
		}

		public void OrderBy(SortCriteria sortCriteria) {
			_ = sortCriteria switch {
				SortCriteria.SONG => OrderByName(),
				SortCriteria.ARTIST => OrderByArtist(),
				SortCriteria.SOURCE => OrderBySource(),
				SortCriteria.YEAR => OrderByYear(),
				SortCriteria.GENRE => OrderByGenre(),
				SortCriteria.ALBUM => OrderByAlbum(),
				SortCriteria.CHARTER => OrderByCharter(),
				_ => OrderByName()
			};
		}

		public bool OrderByName() {
			index = song => {
				string name = song.SongEntry.NameNoParenthesis;
				return GetFirstCharacter(name);
			};

			sortBy = song => {
				return song.NameNoParenthesis.ToUpper();
			};

			return true;
		}

		public bool OrderByArtist() {
			index = song => {
				string artist = song.SongEntry.Artist;
				return GetFirstCharacter(artist);
			};

			sortBy = song => {
				return song.Artist.ToUpper();
			};

			return true;
		}

		public bool OrderBySource() {
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

		public bool OrderByAlbum() {
			index = song => {
				string album = song.SongEntry.Album;
				return GetFirstCharacter(album);
			};

			sortBy = song => {
				return song.Album.ToUpper();
			};

			return true;
		}

		public bool OrderByCharter() {
			index = song => {
				string charter = song.SongEntry.Charter;
				return GetFirstCharacter(charter);
			};

			sortBy = song => {
				return song.Charter.ToUpper();
			};

			return true;
		}

		public bool OrderByGenre() {
			index = song => {
				string genre = song.SongEntry.Genre;
				return GetFirstCharacter(genre);
			};

			sortBy = song => {
				return song.Genre.ToUpper();
			};

			return true;
		}
		
		public bool OrderByYear() {
			index = song => {
				string year = song.SongEntry.Year;
				return GetYear(year);
			};

			sortBy = song => {
				return song.Year.ToUpper();
			};

			return true;
		}

		private static string GetFirstCharacter(string value) {
			if(string.IsNullOrEmpty(value)){
				return "";
			}

			return value.Substring(0,1).ToUpper();
		}

		private static string GetYear(string value) {
			if(string.IsNullOrEmpty(value)){
				return "0";
			}

			return value.Substring(0,4).ToUpper();
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
				SortCriteria.YEAR => SortCriteria.GENRE,
				SortCriteria.GENRE => SortCriteria.ALBUM,
				SortCriteria.ALBUM => SortCriteria.CHARTER,
				SortCriteria.CHARTER => SortCriteria.SONG,
				_ => SortCriteria.SONG
			};
		}

		public string getNextSortCriteriaButtonName(SortCriteria sortCriteria) {
			return sortCriteria switch {
				SongSorting.SortCriteria.SONG => "Order by Artist",
				SongSorting.SortCriteria.ARTIST => "Order by Source",
				SongSorting.SortCriteria.SOURCE => "Order by Year",
				SongSorting.SortCriteria.YEAR => "Order by Genre",
				SongSorting.SortCriteria.GENRE => "Order by Album",
				SongSorting.SortCriteria.ALBUM => "Order by Charter",
				SongSorting.SortCriteria.CHARTER => "Order by Song",
				_ => "Order by Song"
			};
		}

		private string GetNextLetterOrNumber(SongViewType song){
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

			bool isLast = indexOfActualLetter == (songsFirstLetter.Count - 1);

			if (isLast) {
				var firstCharacterInList = songsFirstLetter[0];
				return firstCharacterInList;
			}

			var nextCharacter = songsFirstLetter[indexOfActualLetter + 1];
			return nextCharacter;
		}

		public int SelectNextSection(List<ViewType> songs, int selectedIndex, SongViewType song, int skip){

			string nextCharacter = GetNextLetterOrNumber(song);

			// If an error occurs no change is made
			if (string.IsNullOrEmpty(nextCharacter)) {
				return selectedIndex;
			}

			var _index = songs.FindIndex(skip, song =>
				song is SongViewType songType &&
					String.Equals(
						index(songType),
						nextCharacter,
						StringComparison.OrdinalIgnoreCase)
				);

			return _index;
		}
	}
}