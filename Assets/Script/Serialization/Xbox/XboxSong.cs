using System;
using System.IO;
using DtxCS.DataTypes;
using UnityEngine;
using YARG.Data;
using YARG.Song;

namespace YARG.Serialization {
	public class XboxSong {
		public string ShortName { get; private set; }
		public string MidiFile { get; private set; }
		public string MidiUpdateFile { get; private set; }

		public string SongFolderPath { get; }

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
			SongFolderPath = Path.Combine(pathName, ShortName);

			// Set midi file
			MidiFile = Path.Combine(SongFolderPath, $"{ShortName}.mid");

			// Parse the mogg
			moggDta = new XboxMoggData(Path.Combine(SongFolderPath, $"{ShortName}.mogg"));
			moggDta.ParseMoggHeader();
			moggDta.ParseFromDta(dta.Array("song"));
			moggDta.CalculateMoggBassInfo();

			// Parse the image
			string imgPath = Path.Combine(SongFolderPath, "gen", $"{ShortName}_keep.png_xbox");
			if (songDta.AlbumArtRequired() && File.Exists(imgPath)) {
				img = new XboxImage(imgPath);
			}
		}

		public void UpdateSong(string pathUpdateName, DataArray dta_update) {
			songDta.ParseFromDta(dta_update);
			// if dta_update.Array("song") is not null, parse for any MoggDta as well
			if (dta_update.Array("song") is DataArray moggUpdateDta)
				moggDta.ParseFromDta(moggUpdateDta);

			// if extra_authoring has disc_update, grab update midi
			if (songDta.discUpdate) {
				MidiUpdateFile = Path.Combine(pathUpdateName, ShortName, $"{ShortName}_update.mid");
			}

			// if update mogg exists, grab it and parse it
			string moggUpdatePath = Path.Combine(pathUpdateName, ShortName, $"{ShortName}_update.mogg");
			if (File.Exists(moggUpdatePath)) {
				moggDta.MoggPath = moggUpdatePath;
				moggDta.ParseMoggHeader();
				// moggDta.ParseFromDta(dta_update.Array("song"));
				moggDta.CalculateMoggBassInfo();
			}

			// if album_art == TRUE AND alternate_path == TRUE, grab update png
			if (songDta.albumArt && songDta.alternatePath) {
				Debug.Log($"new album art, grabbing it now");
				// make a new image here, cuz what if an old one exists?
				img = new XboxImage(Path.Combine(pathUpdateName, ShortName, "gen", $"{ShortName}_keep.png_xbox"));
			}
		}

		public bool IsValidSong() {
			// Skip if the song doesn't have notes
			if (!File.Exists(MidiFile)) {
				return false;
			}

			// Skip if this is a "fake song" (tutorials, etc.)
			if (songDta.IsFake()) {
				return false;
			}

			// Skip if the mogg is encrypted
			if (moggDta.Header != 0xA) {
				return false;
			}

			return true;
		}

		public override string ToString() {
			return string.Join(Environment.NewLine,
				$"XBOX SONG {ShortName}",
				$"song folder path: {SongFolderPath}",
				"",
				songDta.ToString(),
				"",
				moggDta.ToString()
			);
		}

		public void CompleteSongInfo(ExtractedConSongEntry song, bool rb) {
			// Set infos
			song.Name = songDta.name;
			song.Source = songDta.gameOrigin;

			// if the source is UGC/UGC_plus but no "UGC_" in shortname, assume it's a custom
			if (songDta.gameOrigin == "ugc" || songDta.gameOrigin == "ugc_plus") {
				if (!(songDta.shortname.Contains("UGC_"))) {
					song.Source = "customs";
				}
			}

			song.SongLength = (int) songDta.songLength;
			// song.delay
			song.DrumType = rb ? DrumType.FourLane : DrumType.FiveLane;
			if (songDta.hopoThreshold != 0)
				song.HopoThreshold = songDta.hopoThreshold;
			song.Artist = songDta.artist ?? "Unknown Artist";
			song.Album = songDta.albumName;
			song.Genre = songDta.genre;
			// song.charter
			song.Year = songDta.yearReleased?.ToString();
			// song.loadingPhrase

			// Set CON specific info
			song.MoggInfo = moggDta;
			song.ImageInfo = img;

			// Set difficulties
			foreach (var (key, value) in songDta.ranks) {
				var instrument = InstrumentHelper.FromStringName(key);
				if (instrument == Instrument.INVALID) {
					continue;
				}

				song.PartDifficulties[instrument] = DtaDifficulty.ToNumberedDiff(instrument, value);
			}

			// Set pro drums
			song.PartDifficulties[Instrument.REAL_DRUMS] = song.PartDifficulties[Instrument.DRUMS];

			// Set harmony difficulty (if exists)
			if (songDta.vocalParts > 1) {
				song.PartDifficulties[Instrument.HARMONY] = song.PartDifficulties[Instrument.VOCALS];
			}
		}
	}
}