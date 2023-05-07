using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DtxCS;
using DtxCS.DataTypes;
using UnityEngine;
using YARG.Data;
using YARG.Song;

namespace YARG.Serialization {
	public static class XboxRawfileBrowser {
		public static List<XboxSong> BrowseFolder(string folder, string folder_update) {
			var songList = new List<XboxSong>();
			var dtaTree = new DataArray();
			var dtaUpdate = new DataArray();

			// Attempt to read songs.dta
			try {
				using var sr = new StreamReader(Path.Combine(folder, "songs.dta"), Encoding.GetEncoding("iso-8859-1"));
				dtaTree = DTX.FromDtaString(sr.ReadToEnd());

				Debug.Log("Successfully read dta");
			} catch (Exception e) {
				Debug.LogError($"Failed to parse songs.dta for `{folder}`.");
				Debug.LogException(e);
				return null;
			}

			// Attempt to read songs_updates.dta, if update folder was provided
			// if (folder_update != null) {
			// 	try {
			// 		using var sr = new StreamReader(Path.Combine(folder_update, "songs_updates.dta"), Encoding.GetEncoding("iso-8859-1"));
			// 		dtaUpdate = DTX.FromDtaString(sr.ReadToEnd());

			// 		Debug.Log("Successfully read update dta");
			// 	} catch (Exception ee) {
			// 		Debug.LogError($"Failed to parse songs_updates.dta for `{folder_update}`.");
			// 		Debug.LogException(ee);
			// 		return null;
			// 	}
			// }

			// Read each song the dta file lists
			for (int i = 0; i < dtaTree.Count; i++) {
				try {
					var currentArray = (DataArray) dtaTree[i];
					var currentSong = new XboxSong(folder, currentArray);

					// if updates were provided
					// if (folder_update != null) {
					// 	// if dtaUpdate has the matching shortname, update that XboxSong
					// 	if (dtaUpdate.Array(currentSong.ShortName) is DataArray dtaMissing) {
					// 		currentSong.UpdateSong(folder_update, dtaMissing);
					// 	}
					// }

					if (currentSong.IsValidSong()) {
						songList.Add(currentSong);
					} else {
						Debug.LogError($"Song with shortname `{currentSong.ShortName}` is invalid. Skipping.");
					}
				} catch (Exception e) {
					Debug.Log($"Failed to load song, skipping...");
					Debug.LogException(e);
				}
			}

			return songList;
		}

		// public static void BrowseUpdateFolder(string folder, List<XboxSong> baseSongs) {
		// 	var dtaTree = new DataArray();

		// 	// Attempt to read songs_updates.dta
		// 	try {
		// 		using var sr = new StreamReader(Path.Combine(folder, "songs_updates.dta"), Encoding.GetEncoding("iso-8859-1"));
		// 		dtaTree = DTX.FromDtaString(sr.ReadToEnd());

		// 		Debug.Log("Successfully read update dta");
		// 	} catch (Exception e) {
		// 		Debug.LogError($"Failed to parse songs_updates.dta for `{folder}`.");
		// 		Debug.LogException(e);
		// 		return;
		// 	}

		// 	// Read each song the update dta lists
		// 	for (int i = 0; i < dtaTree.Count; i++) {
		// 		Debug.Log(dtaTree[i].Name);
		// 		// if(baseSongs.ShortName)
		// 	}
		// }
	}

	public static class ExCONBrowser {
		public static List<ExtractedConSongEntry> BrowseFolder(string folder){
			var songList = new List<ExtractedConSongEntry>();
			var dtaTree = new DataArray();

			// Attempt to read songs.dta
			try {
				using var sr = new StreamReader(Path.Combine(folder, "songs.dta"), Encoding.GetEncoding("iso-8859-1"));
				dtaTree = DTX.FromDtaString(sr.ReadToEnd());
			} catch (Exception e) {
				Debug.LogError($"Failed to parse songs.dta for `{folder}`.");
				Debug.LogException(e);
				return null;
			}

			// Read each song the dta file lists
			for (int i = 0; i < dtaTree.Count; i++) {
				try {
					var currentArray = (DataArray) dtaTree[i];
					// Parse songs.dta
					// Get song metadata from songs.dta
					var currentSong = DTAParser(currentArray);
					
					// since Location is currently set to the name of the folder before mid/mogg/png, set those paths now
					currentSong.NotesFile = Path.Combine(folder, currentSong.Location, $"{currentSong.Location}.mid");
					currentSong.MoggPath = Path.Combine(folder, currentSong.Location, $"{currentSong.Location}.mogg");
					currentSong.ImagePath = Path.Combine(folder, currentSong.Location, "gen", $"{currentSong.Location}_keep.png_xbox");

					// Get song folder path for mid, mogg, png_xbox
					currentSong.Location = Path.Combine(folder, currentSong.Location);
					
					// Parse the mogg
					using var fs = new FileStream(currentSong.MoggPath, FileMode.Open, FileAccess.Read);
					using var br = new BinaryReader(fs);

					currentSong.MoggHeader = br.ReadInt32();
					currentSong.MoggAddressAudioOffset = br.ReadInt32();
					currentSong.MoggAudioLength = fs.Length - currentSong.MoggAddressAudioOffset;
					MoggBASSInfoGenerator(currentSong, currentArray.Array("song"));

					// will validate the song outside of this class, in SongScanThread.cs
					// so okay to add to song list for now
					songList.Add(currentSong);
				} catch (Exception e) {
					Debug.Log($"Failed to load song, skipping...");
					Debug.LogException(e);
				}
			}

			return songList;
		}

		public static ExtractedConSongEntry DTAParser(DataArray dta){
			var cur = new ExtractedConSongEntry();
			cur.ShortName = dta.Name;
			// Debug.Log($"this shortname: {dta.Name}");
			for (int i = 1; i < dta.Count; i++) {
				DataArray dtaArray = (DataArray) dta[i];
				switch (dtaArray[0].ToString()) {
					case "name": cur.Name = ((DataAtom) dtaArray[1]).Name; break;
					case "artist": cur.Artist = ((DataAtom) dtaArray[1]).Name; break;
					case "author": cur.Charter = ((DataAtom) dtaArray[1]).Name; break;
					case "master":
						if (dtaArray[1] is DataSymbol symMaster)
							cur.IsMaster = (symMaster.Name.ToUpper() == "TRUE");
						else if (dtaArray[1] is DataAtom atmMaster)
							cur.IsMaster = (atmMaster.Int != 0);
						break;
					case "song_id": 
						if (dtaArray[1] is DataAtom atmSongId)
							if (atmSongId.Type == DataType.INT)
								cur.SongID = ((DataAtom) dtaArray[1]).Int;
						break;
					case "song_length": cur.SongLength = ((DataAtom) dtaArray[1]).Int; break;
					case "song": // we just want vocal parts and hopo threshold for songDta
						cur.HopoThreshold = (dtaArray.Array("hopo_threshold") != null) ? ((DataAtom) dtaArray.Array("hopo_threshold")[1]).Int : 0;
						cur.VocalParts = (dtaArray.Array("vocal_parts") != null) ? ((DataAtom) dtaArray.Array("vocal_parts")[1]).Int : 1;
						// get the path of the song files
						if(dtaArray.Array("midi_file") != null){
							string loc = "";
							if(dtaArray.Array("midi_file")[1] is DataSymbol symPath)
								loc = symPath.Name.Split("/")[1];
							
							else if(dtaArray.Array("midi_file")[1] is DataAtom atmPath)
								loc = atmPath.Name.Split("/")[1];
							cur.Location = loc;
						}
						else cur.Location = cur.ShortName;
						break;
					case "anim_tempo": 
						if (dtaArray[1] is DataSymbol symTempo)
							cur.AnimTempo = symTempo.Name switch {
								"kTempoSlow" => 16,
								"kTempoMedium" => 32,
								"kTempoFast" => 64,
								_ => 0,
							};
						else if (dtaArray[1] is DataAtom atom)
							cur.AnimTempo = atom.Int;
						break;
					case "preview":
						cur.PreviewStart = ((DataAtom) dtaArray[1]).Int;
						cur.PreviewEnd = ((DataAtom) dtaArray[2]).Int;
						break;
					case "bank": 
						if (dtaArray[1] is DataSymbol symBank)
							cur.VocalPercussionBank = symBank.Name;
						else if (dtaArray[1] is DataAtom atmBank)
							cur.VocalPercussionBank = atmBank.String;
						break;
					case "song_scroll_speed": cur.VocalSongScrollSpeed = ((DataAtom) dtaArray[1]).Int; break;
					case "solo": break; //indicates which instruments have solos: not currently used for YARG
					case "rank":
						for(int j = 1; j < dtaArray.Count; j++){
							if (dtaArray[j] is DataArray inner){
								var inst = InstrumentHelper.FromStringName(((DataSymbol) inner[0]).Name);
								if(inst == Instrument.INVALID) continue;
								cur.PartDifficulties[inst] = DtaDifficulty.ToNumberedDiff(inst, ((DataAtom) inner[1]).Int);
							}
						}
						break;
					case "game_origin": 
						cur.Source = ((DataSymbol) dtaArray[1]).Name; 
						// if the source is UGC/UGC_plus but no "UGC_" in shortname, assume it's a custom
						if(cur.Source == "ugc" || cur.Source == "ugc_plus"){
							if(!(cur.ShortName.Contains("UGC_"))){
								cur.Source = "customs";
							}
						}
						// if the source is any official RB game or its DLC, charter = Harmonix
						if(cur.Source == "rb1" || cur.Source == "rb1_dlc" || cur.Source == "rb1dlc" ||
							cur.Source == "gdrb" || cur.Source == "greenday" || cur.Source == "beatles" ||
							cur.Source == "tbrb" || cur.Source == "lego" || cur.Source == "lrb" || 
							cur.Source == "rb2" || cur.Source == "rb3" || cur.Source == "rb3_dlc" || cur.Source == "rb3dlc"){
							cur.Charter = "Harmonix";
						}
						break;
					case "genre": cur.Genre = ((DataSymbol) dtaArray[1]).Name; break;
					case "rating": cur.SongRating = ((DataAtom) dtaArray[1]).Int; break;
					case "vocal_gender": cur.VocalGender = (((DataSymbol) dtaArray[1]).Name == "male"); break;
					case "fake": cur.IsFake = (dtaArray[1].ToString().ToUpper() == "TRUE"); break;
					case "album_art":
						if (dtaArray[1] is DataSymbol symArt)
							cur.HasAlbumArt = (symArt.Name.ToUpper() == "TRUE");
						else if (dtaArray[1] is DataAtom atmArt)
							cur.HasAlbumArt = (atmArt.Int != 0);
						break;
					case "album_name": cur.Album = ((DataAtom) dtaArray[1]).Name; break;
					case "album_track_number": cur.AlbumTrack = ((DataAtom) dtaArray[1]).Int; break;
					case "year_released": cur.Year = ((DataAtom) dtaArray[1]).Int.ToString(); break;
					case "year_recorded": cur.Year = ((DataAtom) dtaArray[1]).Int.ToString(); break;
					case "vocal_tonic_note": cur.VocalTonicNote = ((DataAtom) dtaArray[1]).Int; break;
					case "song_tonality": cur.SongTonality = ((((DataAtom) dtaArray[1]).Int) != 0); break;
					case "tuning_offset_cents":
						DataAtom tuningAtom = (DataAtom) dtaArray[1];
						if (tuningAtom.Type == DataType.INT) cur.TuningOffsetCents = ((DataAtom) dtaArray[1]).Int;
						else cur.TuningOffsetCents = (int)((DataAtom) dtaArray[1]).Float;
						break;
					case "real_guitar_tuning":
						DataArray guitarTunes = (DataArray) dtaArray[1];
						cur.RealGuitarTuning = new int[6];
						for (int g = 0; g < 6; g++) cur.RealGuitarTuning[g] = ((DataAtom) guitarTunes[g]).Int;
						break;
					case "real_bass_tuning":
						DataArray bassTunes = (DataArray) dtaArray[1];
						cur.RealBassTuning = new int[4];
						for (int b = 0; b < 4; b++) cur.RealBassTuning[b] = ((DataAtom) bassTunes[b]).Int;
						break;
				}
			}

			// must be done after the above parallel loop due to race issues with ranks and vocalParts
			if(!cur.PartDifficulties.ContainsKey(Instrument.VOCALS) || cur.PartDifficulties[Instrument.VOCALS] == 0) cur.VocalParts = 0;

			return cur;
		}

		public static void MoggBASSInfoGenerator(ExtractedConSongEntry song, DataArray dta){
			var Tracks = new Dictionary<string, int[]>();
			float[] PanData = null, VolumeData = null;
			int[] CrowdChannels = null;
			int ChannelCount = 0;

			for (int i = 1; i < dta.Count; i++) {
				var dtaArray = (DataArray) dta[i];
				switch (dtaArray[0].ToString()) {
					case "tracks":
						var trackArray = (DataArray) dtaArray[1];
						for (int x = 0; x < trackArray.Count; x++) {
							if (trackArray[x] is not DataArray instrArray) continue;
							string key = ((DataSymbol) instrArray[0]).Name;
							int[] val;
							if (instrArray[1] is DataArray trackNums) {
								if (trackNums.Count <= 0) continue;
								val = new int[trackNums.Count];
								for (int y = 0; y < trackNums.Count; y++)
									val[y] = ((DataAtom) trackNums[y]).Int;
								Tracks.Add(key, val);
							} else if (instrArray[1] is DataAtom trackNum) {
								val = new int[1];
								val[0] = trackNum.Int;
								Tracks.Add(key, val);
							}
						}
						break;
					case "pans":
						var panArray = dtaArray[1] as DataArray;
						PanData = new float[panArray.Count];
						for (int p = 0; p < panArray.Count; p++) PanData[p] = ((DataAtom) panArray[p]).Float;
						ChannelCount = panArray.Count;
						break;
					case "vols":
						var volArray = dtaArray[1] as DataArray;
						VolumeData = new float[volArray.Count];
						for (int v = 0; v < volArray.Count; v++) VolumeData[v] = ((DataAtom) volArray[v]).Float;
						break;
					case "crowd_channels":
						CrowdChannels = new int[dtaArray.Count - 1];
						for (int cc = 1; cc < dtaArray.Count; cc++)
							CrowdChannels[cc - 1] = ((DataAtom) dtaArray[cc]).Int;
						break;
				}
			}

			// now that we have all the info we need from dta, calculate BASS info
			var mapped = new bool[ChannelCount];

			// BEGIN BASS Stem Mapping ----------------------------------------------------------------------

			if (Tracks.TryGetValue("drum", out var drumArray)) {
				switch (drumArray.Length) {
					//drum (0 1): stereo kit --> (0 1)
					case 2:
						song.StemMaps[SongStem.Drums] = new[] { drumArray[0], drumArray[1] };
						break;
					//drum (0 1 2): mono kick, stereo snare/kit --> (0) (1 2)
					case 3:
						song.StemMaps[SongStem.Drums1] = new[] { drumArray[0] };
						song.StemMaps[SongStem.Drums2] = new[] { drumArray[1], drumArray[2] };
						break;
					//drum (0 1 2 3): mono kick, mono snare, stereo kit --> (0) (1) (2 3)
					case 4:
						song.StemMaps[SongStem.Drums1] = new[] { drumArray[0] };
						song.StemMaps[SongStem.Drums2] = new[] { drumArray[1] };
						song.StemMaps[SongStem.Drums3] = new[] { drumArray[2], drumArray[3] };
						break;
					//drum (0 1 2 3 4): mono kick, stereo snare, stereo kit --> (0) (1 2) (3 4)
					case 5:
						song.StemMaps[SongStem.Drums1] = new[] { drumArray[0] };
						song.StemMaps[SongStem.Drums2] = new[] { drumArray[1], drumArray[2] };
						song.StemMaps[SongStem.Drums3] = new[] { drumArray[3], drumArray[4] };
						break;
					//drum (0 1 2 3 4 5): stereo kick, stereo snare, stereo kit --> (0 1) (2 3) (4 5)
					case 6:
						song.StemMaps[SongStem.Drums1] = new[] { drumArray[0], drumArray[1] };
						song.StemMaps[SongStem.Drums2] = new[] { drumArray[2], drumArray[3] };
						song.StemMaps[SongStem.Drums3] = new[] { drumArray[4], drumArray[5] };
						break;
				}

				foreach (int arr in drumArray) {
					mapped[arr] = true;
				}
			}

			if (Tracks.TryGetValue("bass", out var bassArray)) {
				song.StemMaps[SongStem.Bass] = new int[bassArray.Length];
				for (int i = 0; i < bassArray.Length; i++) {
					song.StemMaps[SongStem.Bass][i] = bassArray[i];
					mapped[bassArray[i]] = true;
				}
			}

			if (Tracks.TryGetValue("guitar", out var gtrArray)) {
				song.StemMaps[SongStem.Guitar] = new int[gtrArray.Length];
				for (int i = 0; i < gtrArray.Length; i++) {
					song.StemMaps[SongStem.Guitar][i] = gtrArray[i];
					mapped[gtrArray[i]] = true;
				}
			}

			if (Tracks.TryGetValue("vocals", out var voxArray)) {
				song.StemMaps[SongStem.Vocals] = new int[voxArray.Length];
				for (int i = 0; i < voxArray.Length; i++) {
					song.StemMaps[SongStem.Vocals][i] = voxArray[i];
					mapped[voxArray[i]] = true;
				}
			}

			if (Tracks.TryGetValue("keys", out var keysArray)) {
				song.StemMaps[SongStem.Keys] = new int[keysArray.Length];
				for (int i = 0; i < keysArray.Length; i++) {
					song.StemMaps[SongStem.Keys][i] = keysArray[i];
					mapped[keysArray[i]] = true;
				}
			}

			if (CrowdChannels != null) {
				song.StemMaps[SongStem.Crowd] = new int[CrowdChannels.Length];
				for (int i = 0; i < CrowdChannels.Length; i++) {
					song.StemMaps[SongStem.Crowd][i] = CrowdChannels[i];
					mapped[CrowdChannels[i]] = true;
				}
			}

			// every index in mapped that is still false, goes in the backing
			var fakeIndices = Enumerable.Range(0, mapped.Length).Where(i => !mapped[i]).ToList();
			song.StemMaps[SongStem.Song] = new int[fakeIndices.Count];
			for (int i = 0; i < fakeIndices.Count; i++) {
				song.StemMaps[SongStem.Song][i] = fakeIndices[i];
			}

			// END BASS Stem Mapping ------------------------------------------------------------------------

			// BEGIN BASS Matrix calculation ----------------------------------------------------------------

			song.MatrixRatios = new float[PanData.Length, 2];

			for(int i = 0; i < PanData.Length; i++){
				float theta = PanData[i] * ((float) Math.PI / 4);
				float ratioL = (float) (Math.Sqrt(2) / 2) * ((float) Math.Cos(theta) - (float) Math.Sin(theta));
				float ratioR = (float) (Math.Sqrt(2) / 2) * ((float) Math.Cos(theta) + (float) Math.Sin(theta));

				float volRatio = (float) Math.Pow(10, VolumeData[i] / 20);

				song.MatrixRatios[i, 0] = volRatio * ratioL;
				song.MatrixRatios[i, 1] = volRatio * ratioR;
			}

			// END BASS Matrix calculation ------------------------------------------------------------------

		}

	}
}