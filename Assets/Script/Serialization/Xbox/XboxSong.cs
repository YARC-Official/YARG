using System;
using System.IO;
using DtxCS.DataTypes;
using YARG.Data;

namespace YARG.Serialization {
	public class XboxSong {
		public string ShortName { get; private set; }
		public string MidiFile { get; private set; }

		private string songFolderPath;

		private XboxSongData songDta;
		private XboxMoggData moggDta;
		private XboxImage img;

		public XboxSong(string pathName, DataArray dta) {
			// Parse songs.dta
			songDta = new XboxSongData();

			// Get song metadata from songs.dta
			songDta.ParseFromDta(dta);
			ShortName = songDta.GetShortName();

			// Get song folder path for mid, mogg, png_xbox
			songFolderPath = Path.Combine(pathName, ShortName);

			// Set midi file
			MidiFile = Path.Combine(songFolderPath, $"{ShortName}.mid");

			// Parse the mogg
			moggDta = new XboxMoggData(Path.Combine(songFolderPath, $"{ShortName}.mogg"));
			moggDta.ParseMoggHeader();
			moggDta.ParseFromDta(dta.Array("song"));
			moggDta.CalculateMoggBASSInfo();

			// Parse the image
			string imgPath = Path.Combine("songFolderPath", "gen", $"{ShortName}_keep.png_xbox");
			if (songDta.AlbumArtRequired() && File.Exists(imgPath)) {
				img = new XboxImage(imgPath);

				// Do some preliminary parsing here in the header to get DXT format, width and height, etc
				img.ParseImageHeader();
			}
		}

		public bool IsValidateSong() {
			// Skip if the song doesn't have notes
			if (!File.Exists(MidiFile)) {
				return false;
			}

			// Skip if this is a "fake song" (tutorials, etc.)
			if (songDta.IsFake()) {
				return false;
			}

			// Skip if the mogg is encrypted
			if (moggDta.GetHeaderVersion() != 0xA) {
				return false;
			}

			return true;
		}

		public override string ToString() {
			return string.Join(Environment.NewLine,
				$"XBOX SONG {ShortName}",
				$"song folder path: {songFolderPath}",
				"",
				songDta.ToString(),
				"",
				moggDta.ToString()
			);
		}

		public void CompleteSongInfo(SongInfo song, bool rb) {
			if (song.fetched) {
				return;
			}
			song.fetched = true;

			// Set infos
			song.SongName = songDta.name;
			song.source = songDta.gameOrigin;
			song.songLength = songDta.songLength / 1000f;
			// song.delay
			song.drumType = rb ? SongInfo.DrumType.FOUR_LANE : SongInfo.DrumType.FIVE_LANE;
			// song.hopoFreq
			song.artistName = songDta.artist;
			song.album = songDta.albumName;
			song.genre = songDta.genre;
			// song.charter
			song.year = songDta.yearReleased?.ToString();
			// song.loadingPhrase

			song.moggInfo = moggDta;
		}
	}
}