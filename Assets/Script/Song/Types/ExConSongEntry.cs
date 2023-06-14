using System.IO;
using UnityEngine;
using DtxCS.DataTypes;
using System.Collections.Generic;
using YARG.Audio;
using YARG.Data;
using YARG.Serialization;
using XboxSTFS;
using System;

namespace YARG.Song {
	public class ExtractedConSongEntry : SongEntry {

		// songs.dta content exclusive to a RB CON
		// NOTE: none of these are written to a cache
		// make sure once these variables start getting used for YARG,
		// that they get cached a la CacheHelpers.cs
		public string ShortName { get; set; }
		public int SongID { get; set; }
		public int AnimTempo { get; set; }
		public string VocalPercussionBank { get; set; }
		public int VocalSongScrollSpeed { get; set; }
		public int SongRating { get; set; } // 1 = FF; 2 = SR; 3 = M; 4 = NR
		public bool VocalGender { get; set; } //true for male, false for female
		public bool HasAlbumArt { get; set; }
		public bool IsFake { get; set; }
		public int VocalTonicNote { get; set; }
		public bool SongTonality { get; set; } // 0 = major, 1 = minor
		public int TuningOffsetCents { get; set; }

		// _update.mid info, if it exists
		public bool DiscUpdate { get; set; } = false;
		public string UpdateMidiPath { get; set; } = string.Empty;

		// pro upgrade info, if it exists
		public SongProUpgrade SongUpgrade { get; private set; }
		public int[] RealGuitarTuning { get; set; }
		public int[] RealBassTuning { get; set; }

		// .mogg info
		public bool UsingUpdateMogg { get; set; } = false;
		public string MoggPath { get; set; }

		public Dictionary<SongStem, int[]> StemMaps { get; set; } = new();
		public float[,] MatrixRatios { get; set; }

		// .milo info
		public bool UsingUpdateMilo { get; set; } = false;
		public string MiloPath { get; set; }
		public int VenueVersion { get; set; }

		// image info
		public bool AlternatePath { get; set; } = false;
		public string ImagePath { get; set; } = string.Empty;

		protected ExtractedConSongEntry(BinaryReader reader, string folder) : base(reader, folder) {}

		public ExtractedConSongEntry(BinaryReader reader, List<XboxSTFSFile> conFiles, string folder) : base(reader, folder) {
			ContinueCacheRead(reader, conFiles);

			// mogg data
			UsingUpdateMogg = reader.ReadBoolean();
			MoggPath = reader.ReadString();
			ImagePath = reader.ReadString();
			HasAlbumArt = ImagePath.Length > 0;
		}

		protected void ContinueCacheRead(BinaryReader reader, List<XboxSTFSFile> conFiles) {
			// update midi data
			DiscUpdate = reader.ReadBoolean();
			UpdateMidiPath = reader.ReadString();

			// pro upgrade data
			if (reader.ReadBoolean())
				SongUpgrade = new SongProUpgrade(reader, conFiles);

			int guitarTuneLength = reader.ReadInt32();
			if (guitarTuneLength > 0) {
				RealGuitarTuning = new int[guitarTuneLength];
				for (int i = 0; i < guitarTuneLength; i++)
					RealGuitarTuning[i] = reader.ReadInt32();
			}
			int bassTuneLength = reader.ReadInt32();
			if (bassTuneLength > 0) {
				RealBassTuning = new int[bassTuneLength];
				for (int i = 0; i < bassTuneLength; i++)
					RealBassTuning[i] = reader.ReadInt32();
			}

			// read milo data
			UsingUpdateMilo = reader.ReadBoolean();
			MiloPath = reader.ReadString();
			VenueVersion = reader.ReadInt32();

			// Read Stem Data
			int stemCount = reader.ReadInt32();
			StemMaps = new Dictionary<SongStem, int[]>();
			for (int i = 0; i < stemCount; i++) {
				var stem = (SongStem)reader.ReadInt32();
				int stemLength = reader.ReadInt32();
				var stemMap = new int[stemLength];
				for (int j = 0; j < stemLength; j++) {
					stemMap[j] = reader.ReadInt32();
				}
				StemMaps.Add(stem, stemMap);
			}

			// Read Matrix Data
			int matrixRowCount = reader.ReadInt32();
			int matrixColCount = reader.ReadInt32();
			MatrixRatios = new float[matrixRowCount, matrixColCount];
			for (int i = 0; i < matrixRowCount; i++) {
				for (int j = 0; j < matrixColCount; j++) {
					MatrixRatios[i, j] = reader.ReadSingle();
				}
			}
		}

		public override void WriteMetadataToCache(BinaryWriter writer) {
			WriteMetadataToCache(writer, SongType.ExtractedRbCon);
			// mogg data
			writer.Write(UsingUpdateMogg);
			writer.Write(MoggPath);
			writer.Write(ImagePath);
		}

		protected void WriteMetadataToCache(BinaryWriter writer, SongType type) {
			writer.Write((int) type);
			base.WriteMetadataToCache(writer);
			// update midi data
			writer.Write(DiscUpdate);
			writer.Write(UpdateMidiPath);

			// pro upgrade data
			writer.Write(SongUpgrade != null);
			if (SongUpgrade != null)
				SongUpgrade.WriteToCache(writer);

			if (RealGuitarTuning != null) {
				writer.Write(RealGuitarTuning.Length);
				for (int i = 0; i < 6; i++)
					writer.Write(RealGuitarTuning[i]);
			} else
				writer.Write(0);

			if (RealBassTuning != null) {
				writer.Write(RealBassTuning.Length);
				for (int i = 0; i < 4; i++)
					writer.Write(RealBassTuning[i]);
			} else
				writer.Write(0);

			// write milo data
			writer.Write(UsingUpdateMilo);
			writer.Write(MiloPath);
			writer.Write(VenueVersion);

			// Write Stem Data
			writer.Write(StemMaps.Count);
			foreach (var stem in StemMaps) {
				writer.Write((int) stem.Key);
				writer.Write(stem.Value.Length);
				foreach (int i in stem.Value) {
					writer.Write(i);
				}
			}

			// Write Matrix Data
			writer.Write(MatrixRatios.GetLength(0));
			writer.Write(MatrixRatios.GetLength(1));
			for (int i = 0; i < MatrixRatios.GetLength(0); i++) {
				for (int j = 0; j < MatrixRatios.GetLength(1); j++) {
					writer.Write(MatrixRatios[i, j]);
				}
			}
		}

		protected ExtractedConSongEntry(DataArray dta) {
			SetFromDTA(dta);
		}

		public virtual bool ValidateMidiFile() {
			return File.Exists(NotesFile);
		}

		public void FinishScan(string cache, string checksum, ulong tracks) {
			CacheRoot = cache;
			Checksum = checksum;
			AvailableParts = tracks;
		}

		public ExtractedConSongEntry(string folder, DataArray dta) : this(dta) {
			string dir = Path.Combine(folder, Location);
			NotesFile = Path.Combine(dir, $"{Location}.mid");

			MoggPath = Path.Combine(dir, $"{Location}.mogg");

			string miloPath = Path.Combine(dir, "gen", $"{Location}.milo_xbox");
			if(File.Exists(miloPath))
				MiloPath = miloPath;

			string imgPath = Path.Combine(dir, "gen", $"{Location}_keep.png_xbox");
			if (File.Exists(imgPath))
				ImagePath = imgPath;

			Location = dir;
		}

		public void Upgrade(List<(SongProUpgrade, DataArray)> upgrades) {
			foreach (var upgr in upgrades) {
				if (upgr.Item2.Name == ShortName) {
					SongUpgrade = upgr.Item1;
					SetFromDTA(upgr.Item2);
					break;
				}
			}
		}

		public void SetFromDTA(DataArray dta) {
			ShortName = dta.Name;
			// Debug.Log($"this shortname: {dta.Name}");
			for (int i = 1; i < dta.Count; i++) {
				DataArray dtaArray = (DataArray) dta[i];
				switch (dtaArray[0].ToString()) {
					case "name": Name = ((DataAtom) dtaArray[1]).Name; break;
					case "artist": Artist = ((DataAtom) dtaArray[1]).Name; break;
					case "author": Charter = ((DataAtom) dtaArray[1]).Name; break;
					case "master":
						if (dtaArray[1] is DataSymbol symMaster)
							IsMaster = (symMaster.Name.ToUpper() == "TRUE");
						else if (dtaArray[1] is DataAtom atmMaster)
							IsMaster = (atmMaster.Int != 0);
						break;
					case "song_id":
						if (dtaArray[1] is DataAtom atmSongId)
							if (atmSongId.Type == DataType.INT)
								SongID = ((DataAtom) dtaArray[1]).Int;
						break;
					case "song_length": SongLength = ((DataAtom) dtaArray[1]).Int; break;
					case "song": // we just want vocal parts and hopo threshold for songDta
						if (dtaArray.Array("hopo_threshold") != null)
							HopoThreshold = ((DataAtom) dtaArray.Array("hopo_threshold")[1]).Int;
						if (dtaArray.Array("vocal_parts") != null)
							VocalParts = ((DataAtom) dtaArray.Array("vocal_parts")[1]).Int;
						// get the path of the song files
						if (dtaArray.Array("name") != null) {
							if (dtaArray.Array("name")[1] is DataSymbol symPath)
								Location = symPath.Name.Split("/")[1];
							else if (dtaArray.Array("name")[1] is DataAtom atmPath)
								Location = atmPath.Name.Split("/")[1];
						} else Location = ShortName;
						break;
					case "anim_tempo":
						if (dtaArray[1] is DataSymbol symTempo)
							AnimTempo = symTempo.Name switch {
								"kTempoSlow" => 16,
								"kTempoMedium" => 32,
								"kTempoFast" => 64,
								_ => 0,
							};
						else if (dtaArray[1] is DataAtom atom)
							AnimTempo = atom.Int;
						break;
					case "preview":
						PreviewStart = ((DataAtom) dtaArray[1]).Int;
						PreviewEnd = ((DataAtom) dtaArray[2]).Int;
						break;
					case "bank":
						if (dtaArray[1] is DataSymbol symBank)
							VocalPercussionBank = symBank.Name;
						else if (dtaArray[1] is DataAtom atmBank)
							VocalPercussionBank = atmBank.String;
						break;
					case "song_scroll_speed": VocalSongScrollSpeed = ((DataAtom) dtaArray[1]).Int; break;
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
								BandDifficulty = DtaDifficulty.ToNumberedDiffForBand(dtaDiff);
								continue;
							}

							var inst = InstrumentHelper.FromStringName(name);
							if (inst == Instrument.INVALID) {
								continue;
							}

							PartDifficulties[inst] = DtaDifficulty.ToNumberedDiff(inst, dtaDiff);
						}

						// Set pro drums
						if (PartDifficulties.ContainsKey(Instrument.DRUMS)) {
							PartDifficulties[Instrument.REAL_DRUMS] = PartDifficulties[Instrument.DRUMS];
						}
						break;
					case "game_origin":
						Source = ((DataSymbol) dtaArray[1]).Name;
						// if the source is UGC/UGC_plus but no "UGC_" in shortname, assume it's a custom
						if (Source == "ugc" || Source == "ugc_plus") {
							if (!(ShortName.Contains("UGC_"))) {
								Source = "customs";
							}
						}

						// if the source is any official RB game or its DLC, charter = Harmonix
						if (SongSources.GetSource(Source).Type == SongSources.SourceType.RB) {
							Charter = "Harmonix";
						}

						// if the source is meant for usage in TBRB, it's a master track
						// TODO: NEVER assume localized version contains "Beatles"
						if(SongSources.SourceToGameName(Source).Contains("Beatles")) IsMaster = true;
						break;
					case "genre": Genre = ((DataSymbol) dtaArray[1]).Name; break;
					case "rating": SongRating = ((DataAtom) dtaArray[1]).Int; break;
					case "vocal_gender": VocalGender = (((DataSymbol) dtaArray[1]).Name == "male"); break;
					case "fake": IsFake = (dtaArray[1].ToString().ToUpper() == "TRUE"); break;
					case "album_art":
						if (dtaArray[1] is DataSymbol symArt)
							HasAlbumArt = (symArt.Name.ToUpper() == "TRUE");
						else if (dtaArray[1] is DataAtom atmArt)
							HasAlbumArt = (atmArt.Int != 0);
						break;
					case "album_name": Album = ((DataAtom) dtaArray[1]).Name; break;
					case "album_track_number": AlbumTrack = ((DataAtom) dtaArray[1]).Int; break;
					case "year_released": Year = ((DataAtom) dtaArray[1]).Int.ToString(); break;
					case "year_recorded": Year = ((DataAtom) dtaArray[1]).Int.ToString(); break;
					case "vocal_tonic_note": VocalTonicNote = ((DataAtom) dtaArray[1]).Int; break;
					case "song_tonality": SongTonality = ((((DataAtom) dtaArray[1]).Int) != 0); break;
					case "tuning_offset_cents":
						DataAtom tuningAtom = (DataAtom) dtaArray[1];
						if (tuningAtom.Type == DataType.INT) TuningOffsetCents = ((DataAtom) dtaArray[1]).Int;
						else TuningOffsetCents = (int) ((DataAtom) dtaArray[1]).Float;
						break;
					case "real_guitar_tuning":
						DataArray guitarTunes = (DataArray) dtaArray[1];
						RealGuitarTuning = new int[6];
						for (int g = 0; g < 6; g++) RealGuitarTuning[g] = ((DataAtom) guitarTunes[g]).Int;
						break;
					case "real_bass_tuning":
						DataArray bassTunes = (DataArray) dtaArray[1];
						RealBassTuning = new int[4];
						for (int b = 0; b < 4; b++) RealBassTuning[b] = ((DataAtom) bassTunes[b]).Int;
						break;
					case "version": VenueVersion = ((DataAtom) dtaArray[1]).Int; break;
					case "alternate_path":
						if (dtaArray[1] is DataSymbol symAltPath)
							AlternatePath = (symAltPath.Name.ToUpper() == "TRUE");
						else if (dtaArray[1] is DataAtom atmAltPath)
							AlternatePath = (atmAltPath.Int != 0);
						break;
					case "extra_authoring":
						for (int ea = 1; ea < dtaArray.Count; ea++) {
							if (dtaArray[ea] is DataSymbol symEA) {
								if (symEA.Name == "disc_update") {
									DiscUpdate = true;
									break;
								}
							} else if (dtaArray[ea] is DataAtom atmEA) {
								if (atmEA.String == "disc_update") {
									DiscUpdate = true;
									break;
								}
							}
						}
						break;
				}
			}

			// must be done after the above parallel loop due to race issues with ranks and vocalParts
			if (PartDifficulties.TryGetValue(Instrument.VOCALS, out var voxRank)) {
				// at least one vocal part exists
				if (voxRank != -1) {
					if (VocalParts == 0) {
						// the default value of a SongEntry (i.e., no harmonies found)
						VocalParts = 1;
					} else {
						// since vocal parts != 0, we know vocal_parts was parsed earlier - so there's harmonies - set difficulty
						PartDifficulties[Instrument.HARMONY] = PartDifficulties[Instrument.VOCALS];
					}
				}
			}
		}

		virtual public void Update(string folder) {
			string dir = Path.Combine(folder, ShortName);
			if (DiscUpdate) {
				string updateMidiPath = Path.Combine(dir, $"{ShortName}_update.mid");
				if (File.Exists(updateMidiPath))
					UpdateMidiPath = updateMidiPath;
				else {
					Debug.LogError($"Couldn't update song {ShortName} - update file {UpdateMidiPath} not found!");
					DiscUpdate = false; // to prevent breaking in-game if the user still tries to play the song
				}
			}

			string updateMoggPath = Path.Combine(dir, $"{ShortName}_update.mogg");
			if (File.Exists(updateMoggPath)) {
				UsingUpdateMogg = true;
				MoggPath = updateMoggPath;
			}

			string updateMiloPath = Path.Combine(dir, "gen", $"{ShortName}.milo_xbox");
			if(File.Exists(updateMiloPath)){
				UsingUpdateMilo = true;
				MiloPath = updateMiloPath;
			}

			string imgUpdatePath = Path.Combine(dir, "gen", $"{ShortName}_keep.png_xbox");
			if (HasAlbumArt && AlternatePath) {
				if (File.Exists(imgUpdatePath))
					ImagePath = imgUpdatePath;
				else
					AlternatePath = false;
			}
		}

		public virtual byte[] LoadMidiFile() {
			return File.ReadAllBytes(NotesFile);
		}

		public virtual byte[] LoadMoggFile() {
			return File.ReadAllBytes(MoggPath);
		}

		public virtual byte[] LoadMiloFile(){
			if(MiloPath.Length == 0)
				return Array.Empty<byte>();
			return File.ReadAllBytes(MiloPath);
		}

		public virtual byte[] LoadImgFile() {
			if (!HasAlbumArt || ImagePath.Length == 0)
				return Array.Empty<byte>();
			return File.ReadAllBytes(ImagePath);
		}

		public virtual bool IsMoggUnencrypted() {
			var fs = new FileStream(MoggPath, FileMode.Open, FileAccess.Read);
			return fs.ReadInt32LE() == 0xA;
		}

		public byte[] LoadMidiUpdateFile() {
			return File.ReadAllBytes(UpdateMidiPath);
		}
	}
}