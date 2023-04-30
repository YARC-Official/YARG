using System;
using System.Collections.Generic;
using DtxCS.DataTypes;
using UnityEngine;

// complete GH DX songs.dta additions:
// artist (songalbum, author (as in chart author), songyear, songgenre, songorigin, 
//      songduration, songguitarrank, songbassrank, songrhythmrank, songdrumrank, songartist)

// common attributes:
// shortname, name, artist, master/caption,
// song (name, tracks, pans, vols, cores)
// anim_tempo, preview

namespace YARG.Serialization {
	public class XboxSongData {
		// all the possible metadata you could possibly want from a particular songs.dta
		public string shortname, name, artist;
		public string gameOrigin, genre, albumName, bank;
		public string ghOutfit, ghGuitar, ghVenue;
		public uint songLength = 0, songScrollSpeed = 2300;
		public short tuningOffsetCents = 0;
		public uint? songId;
		public ushort? yearReleased, yearRecorded, vocalTonicNote; //TODO: implement other nullable/optional variables with default values
		public bool? songTonality;
		public bool master = false, albumArt = false, vocalGender; //vocalGender is true if male, false if female
		public byte rating = 4, animTempo;
		public byte? albumTrackNumber;
		public byte vocalParts = 1;
		public bool fake = false;
		public bool alternatePath = false;
		public bool discUpdate = false;
		public (uint start, uint end) preview;
		public short[] realGuitarTuning, realBassTuning;
		public string[] solos;
		public Dictionary<string, ushort> ranks;
		public int hopoThreshold = 0;

		//TODO: implement macro support, such as #ifndef kControllerRealGuitar, or #ifdef YARG
		public void ParseFromDta(DataArray dta) {
			shortname = dta.Name;
			for (int i = 1; i < dta.Count; i++) {
				DataArray dtaArray = (DataArray) dta[i];
				switch (dtaArray[0].ToString()) {
					case "name": name = ((DataAtom) dtaArray[1]).Name; break;
					case "artist": artist = ((DataAtom) dtaArray[1]).Name; break;
					case "master":
						if (dtaArray[1] is DataSymbol symMaster)
							master = (symMaster.Name.ToUpper() == "TRUE");
						else if (dtaArray[1] is DataAtom atmMaster)
							master = (atmMaster.Int != 0);
						break;
					case "caption": master = true; break; //used in GH
					case "song_id":
						if (dtaArray[1] is DataAtom atmSongId)
							if (atmSongId.Type == DataType.INT)
								songId = (uint) ((DataAtom) dtaArray[1]).Int;
						break;
					case "song_length": songLength = (uint) ((DataAtom) dtaArray[1]).Int; break;
					case "song": // we just want vocal parts and hopo threshold for songDta
						hopoThreshold = (dtaArray.Array("hopo_threshold") != null) ? ((DataAtom) dtaArray.Array("hopo_threshold")[1]).Int : 0;
						vocalParts = (dtaArray.Array("vocal_parts") != null) ? (byte) ((DataAtom) dtaArray.Array("vocal_parts")[1]).Int : (byte) 1;
						break;
					case "anim_tempo":
						if (dtaArray[1] is DataSymbol symTempo)
							animTempo = symTempo.Name switch {
								"kTempoSlow" => 16,
								"kTempoMedium" => 32,
								"kTempoFast" => 64,
								_ => 0,
							};
						else if (dtaArray[1] is DataAtom atom)
							animTempo = (byte) atom.Int;
						break;
					case "preview":
						preview = ((uint) ((DataAtom) dtaArray[1]).Int, (uint) ((DataAtom) dtaArray[2]).Int);
						break;
					case "bank":
						if (dtaArray[1] is DataSymbol symBank)
							bank = symBank.Name;
						else if (dtaArray[1] is DataAtom atmBank)
							bank = atmBank.String;
						break;
					case "song_scroll_speed": songScrollSpeed = (uint) ((DataAtom) dtaArray[1]).Int; break;
					case "solo":
						DataArray soloInstruments = (DataArray) dtaArray[1];
						solos = new string[soloInstruments.Count];
						for (int t = 0; t < soloInstruments.Count; t++)
							if (soloInstruments[t] is DataSymbol symSolo)
								solos[t] = symSolo.Name;
						break;
					case "rank":
						ranks = new Dictionary<string, ushort>();
						for (int j = 1; j < dtaArray.Count; j++)
							if (dtaArray[j] is DataArray inner)
								ranks.Add(((DataSymbol) inner[0]).Name, (ushort) ((DataAtom) inner[1]).Int);
						break;
					case "game_origin": gameOrigin = ((DataSymbol) dtaArray[1]).Name; break;
					case "genre": genre = ((DataSymbol) dtaArray[1]).Name; break;
					case "rating": rating = (byte) ((DataAtom) dtaArray[1]).Int; break;
					case "vocal_gender": vocalGender = (((DataSymbol) dtaArray[1]).Name == "male"); break;
					case "fake": fake = (dtaArray[1].ToString().ToUpper() == "TRUE"); break;
					case "album_art":
						if (dtaArray[1] is DataSymbol symArt)
							albumArt = (symArt.Name.ToUpper() == "TRUE");
						else if (dtaArray[1] is DataAtom atmArt)
							albumArt = (atmArt.Int != 0);
						break;
					case "album_name": albumName = ((DataAtom) dtaArray[1]).Name; break;
					case "album_track_number": albumTrackNumber = (byte) ((DataAtom) dtaArray[1]).Int; break;
					case "year_released": yearReleased = (ushort) ((DataAtom) dtaArray[1]).Int; break;
					case "year_recorded": yearRecorded = (ushort) ((DataAtom) dtaArray[1]).Int; break;
					case "vocal_tonic_note": vocalTonicNote = (ushort) ((DataAtom) dtaArray[1]).Int; break;
					case "song_tonality": songTonality = ((((DataAtom) dtaArray[1]).Int) != 0); break; //0 = major, 1 = minor
					case "tuning_offset_cents":
						DataAtom tuningAtom = (DataAtom) dtaArray[1];
						if (tuningAtom.Type == DataType.INT) tuningOffsetCents = (short) ((DataAtom) dtaArray[1]).Int;
						else tuningOffsetCents = (short) ((DataAtom) dtaArray[1]).Float;
						break;
					case "real_guitar_tuning":
						DataArray guitarTunes = (DataArray) dtaArray[1];
						realGuitarTuning = new short[6];
						for (int g = 0; g < 6; g++) realGuitarTuning[g] = (short) ((DataAtom) guitarTunes[g]).Int;
						break;
					case "real_bass_tuning":
						DataArray bassTunes = (DataArray) dtaArray[1];
						realBassTuning = new short[4];
						for (int b = 0; b < 4; b++) realBassTuning[b] = (short) ((DataAtom) bassTunes[b]).Int;
						break;
					case "alternate_path":
						if (dtaArray[1] is DataSymbol symAltPath)
							alternatePath = (symAltPath.Name.ToUpper() == "TRUE");
						else if (dtaArray[1] is DataAtom atmAltPath)
							alternatePath = (atmAltPath.Int != 0);
						break;
					case "extra_authoring":
						for(int ea = 1; ea < dtaArray.Count; ea++){
							if(dtaArray[ea] is DataSymbol symEA){
								if(symEA.Name == "disc_update"){
									discUpdate = true;
									break;
								}
							}
							else if(dtaArray[ea] is DataAtom atmEA){
								if(atmEA.String == "disc_update"){
									discUpdate = true;
									break;
								}
							}
						}
						break;
					case "quickplay": //used in GH
						for (int q = 1; q < dtaArray.Count; q++) {
							DataArray innerQPArray = (DataArray) dtaArray[q];
							switch (innerQPArray[0].ToString()) {
								case "character_outfit": ghOutfit = ((DataSymbol) innerQPArray[1]).Name; break;
								case "guitar": ghGuitar = ((DataSymbol) innerQPArray[1]).Name; break;
								case "venue": ghVenue = ((DataSymbol) innerQPArray[1]).Name; break;
							}
						}
						break;
					default:
						break;
				}
			}
			// must be done after the above parallel loop due to race issues with ranks and vocalParts
			if (!ranks.ContainsKey("vocals") || ranks["vocals"] == 0) vocalParts = 0;
		}

		public string GetShortName() { return shortname; }
		public bool AlbumArtRequired() { return albumArt; }
		public bool IsFake() { return fake; }

		public override string ToString() {
			return string.Join(Environment.NewLine,
				$"Song metadata:",
				$"song id={songId}; shortname={shortname}: name={name}; artist={((!master) ? "as made famous by " : "")}{artist}",
				$"vocal parts={vocalParts}; vocal gender={((vocalGender) ? "male" : "female")}",
				$"ranks={string.Join(", ", ranks)}",
				$"album art={albumArt}; album name={albumName}; album track number={albumTrackNumber}",
				$"year released={yearReleased}; year recorded={yearRecorded}",
				$"song length={songLength}; preview={preview}; game origin={gameOrigin}; genre={genre}; rating={rating}",
				$"vocal tonic note={vocalTonicNote}",
				$"song tonality={songTonality}",
				$"tuning offset cents={tuningOffsetCents}",
				$"real guitar tuning=({((realGuitarTuning != null) ? string.Join(", ", realGuitarTuning) : "")})",
				$"real bass tuning=({((realBassTuning != null) ? string.Join(", ", realBassTuning) : "")})"
			);
		}
	}
}