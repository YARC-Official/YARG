using DtxCS.DataTypes;
using YARG.Data;
using YARG.Song;

namespace YARG.Serialization {
	public static class XboxDTAParser {
		public static ConSongEntry ParseFromDta(DataArray dta, ConSongEntry existingSong = null) {
			var cur = existingSong;
			if (existingSong == null) {
				cur = new ConSongEntry();
			}

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
						if (dtaArray.Array("hopo_threshold") != null)
							cur.HopoThreshold = ((DataAtom) dtaArray.Array("hopo_threshold")[1]).Int;
						if (dtaArray.Array("vocal_parts") != null)
							cur.VocalParts = ((DataAtom) dtaArray.Array("vocal_parts")[1]).Int;
						// get the path of the song files
						if (dtaArray.Array("name") != null) {
							if (dtaArray.Array("name")[1] is DataSymbol symPath)
								cur.Location = symPath.Name.Split("/")[1];
							else if (dtaArray.Array("name")[1] is DataAtom atmPath)
								cur.Location = atmPath.Name.Split("/")[1];
						} else cur.Location = cur.ShortName;
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
						for (int j = 1; j < dtaArray.Count; j++) {
							if (dtaArray[j] is not DataArray inner) {
								continue;
							}

							string name = ((DataSymbol) inner[0]).Name;
							int dtaDiff = ((DataAtom) inner[1]).Int;

							// Band difficulty is special
							if (name == "band") {
								cur.BandDifficulty = DtaDifficulty.ToNumberedDiffForBand(dtaDiff);
								continue;
							}

							var inst = InstrumentHelper.FromStringName(name);
							if (inst == Instrument.INVALID) {
								continue;
							}

							cur.PartDifficulties[inst] = DtaDifficulty.ToNumberedDiff(inst, dtaDiff);
						}

						// Set pro drums
						if (cur.PartDifficulties.ContainsKey(Instrument.DRUMS)) {
							cur.PartDifficulties[Instrument.REAL_DRUMS] = cur.PartDifficulties[Instrument.DRUMS];
						}
						break;
					case "game_origin":
						cur.Source = ((DataSymbol) dtaArray[1]).Name;
						// if the source is UGC/UGC_plus but no "UGC_" in shortname, assume it's a custom
						if (cur.Source == "ugc" || cur.Source == "ugc_plus") {
							if (!(cur.ShortName.Contains("UGC_"))) {
								cur.Source = "customs";
							}
						}
						// if the source is any official RB game or its DLC, charter = Harmonix
						if (cur.Source == "rb1" || cur.Source == "rb1_dlc" || cur.Source == "rb1dlc" ||
							cur.Source == "gdrb" || cur.Source == "greenday" || cur.Source == "beatles" ||
							cur.Source == "tbrb" || cur.Source == "lego" || cur.Source == "lrb" ||
							cur.Source == "rb2" || cur.Source == "rb3" || cur.Source == "rb3_dlc" || cur.Source == "rb3dlc") {
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
						else cur.TuningOffsetCents = (int) ((DataAtom) dtaArray[1]).Float;
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
					case "alternate_path":
						if (dtaArray[1] is DataSymbol symAltPath)
							cur.AlternatePath = (symAltPath.Name.ToUpper() == "TRUE");
						else if (dtaArray[1] is DataAtom atmAltPath)
							cur.AlternatePath = (atmAltPath.Int != 0);
						break;
					case "extra_authoring":
						for (int ea = 1; ea < dtaArray.Count; ea++) {
							if (dtaArray[ea] is DataSymbol symEA) {
								if (symEA.Name == "disc_update") {
									cur.DiscUpdate = true;
									break;
								}
							} else if (dtaArray[ea] is DataAtom atmEA) {
								if (atmEA.String == "disc_update") {
									cur.DiscUpdate = true;
									break;
								}
							}
						}
						break;
				}
			}

			// must be done after the above parallel loop due to race issues with ranks and vocalParts
			if (cur.PartDifficulties.TryGetValue(Instrument.VOCALS, out var voxRank)) {
				// at least one vocal part exists
				if (voxRank != -1) {
					if (cur.VocalParts == 0) {
						// the default value of a SongEntry (i.e., no harmonies found)
						cur.VocalParts = 1;
					} else {
						// since vocal parts != 0, we know vocal_parts was parsed earlier - so there's harmonies - set difficulty
						cur.PartDifficulties[Instrument.HARMONY] = cur.PartDifficulties[Instrument.VOCALS];
					}
				}
			}

			return cur;
		}
	}
}