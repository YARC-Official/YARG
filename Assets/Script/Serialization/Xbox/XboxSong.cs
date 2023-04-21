using System;
using System.IO;
using DtxCS.DataTypes;
using YARG.Data;

namespace YARG.Serialization {
	public class XboxSong {
		private string shortname;
		private string songFolderPath;
		private XboxSongData songDta;
		private XboxMoggData moggDta;
		private XboxImage img;

		public XboxSong(string pathName, DataArray dta) {
			// parse songs.dta
			songDta = new XboxSongData();
			songDta.ParseFromDta(dta); // get song metadata from songs.dta
			shortname = songDta.GetShortName();
			songFolderPath = pathName + "/" + shortname; // get song folder path for mid, mogg, png_xbox

			// parse the mogg
			moggDta = new XboxMoggData($"{songFolderPath}/{shortname}.mogg");
			moggDta.ParseMoggHeader();
			moggDta.ParseFromDta(dta.Array("song")); // get mogg metadata from songs.dta
			moggDta.CalculateMoggBASSInfo();

			// parse the image
			string imgPath = $"{songFolderPath}/gen/{shortname}_keep.png_xbox";
			if (songDta.AlbumArtRequired() && File.Exists(imgPath)) {
				img = new XboxImage(imgPath);
				// do some preliminary parsing here in the header to get DXT format, width and height, etc
				img.ParseImageHeader();
			}
		}

		public string GetXboxSongShortname() => shortname;

		// true if this song is good to go and can be shown in-game, false if not
		public bool ValidateSong() => !songDta.IsFake() && (moggDta.GetHeaderVersion() == 0xA);

		public override string ToString() {
			return string.Join(Environment.NewLine,
				$"XBOX SONG {shortname}",
				$"song folder path: {songFolderPath}",
				"",
				songDta.ToString(),
				"",
				moggDta.ToString()
			);
		}

		// TODO: implement this fxn
		// public SongInfo ConvertToSongInfo() {

		// }
	}
}