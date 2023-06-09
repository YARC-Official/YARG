using System;
using System.Collections.Generic;
using System.IO;
using YARG.Data;

namespace YARG.Song {
	public enum SongType {
		SongIni,
		ExtractedRbCon,
		RbCon,
	}

	public enum DrumType {
		FourLane,
		FiveLane,
		Unknown
	}

	public abstract class SongEntry {

		public string CacheRoot { get; protected set; }

		public DrumType DrumType { get; protected set; }

		public string Name { get; protected set; } = string.Empty;

		public string NameNoParenthesis => string.IsNullOrEmpty(Name) ? "" : Name.Replace("(", "").Replace(")", "");

		public string Artist { get; protected set; } = string.Empty;
		public string Charter { get; protected set; } = string.Empty;
		public bool IsMaster { get; protected set; }

		public string Album { get; protected set; } = string.Empty;
		public int AlbumTrack { get; protected set; }

		public int PlaylistTrack { get; protected set; }

		public string Genre { get; protected set; } = string.Empty;
		public string Year { get; protected set; } = string.Empty;

		public int SongLength { get; protected set; }
		public TimeSpan SongLengthTimeSpan => TimeSpan.FromMilliseconds(SongLength);

		public int PreviewStart { get; protected set; }
		public TimeSpan PreviewStartTimeSpan => TimeSpan.FromMilliseconds(PreviewStart);

		public int PreviewEnd { get; protected set; }
		public TimeSpan PreviewEndTimeSpan => TimeSpan.FromMilliseconds(PreviewEnd);

		public double Delay { get; protected set; }

		public string LoadingPhrase { get; protected set; } = string.Empty;

		public int HopoThreshold { get; protected set; } = 170;
		public bool EighthNoteHopo { get; protected set; }
		public int MultiplierNote { get; protected set; }

		public string Source { get; protected set; } = string.Empty;

		public Dictionary<Instrument, int> PartDifficulties { get; protected set; } = new();
		public int BandDifficulty { get; protected set; }

		public ulong AvailableParts { get; protected set; }
		public int VocalParts { get; protected set; }

		public string Checksum { get; protected set; }
		public string NotesFile { get; protected set; }
		public string Location { get; protected set; }

		protected SongEntry(){}

		protected SongEntry(BinaryReader reader, string folder) {
			DrumType = (DrumType) reader.ReadInt32();

			Name = reader.ReadString();
			Artist = reader.ReadString();
			Charter = reader.ReadString();
			IsMaster = reader.ReadBoolean();
			Album = reader.ReadString();
			AlbumTrack = reader.ReadInt32();
			PlaylistTrack = reader.ReadInt32();
			Genre = reader.ReadString();
			Year = reader.ReadString();
			SongLength = reader.ReadInt32();
			PreviewStart = reader.ReadInt32();
			PreviewEnd = reader.ReadInt32();
			Delay = reader.ReadDouble();
			LoadingPhrase = reader.ReadString();
			HopoThreshold = reader.ReadInt32();
			EighthNoteHopo = reader.ReadBoolean();
			MultiplierNote = reader.ReadInt32();
			Source = reader.ReadString();

			// Read difficulties
			int difficultyCount = reader.ReadInt32();
			for (var i = 0; i < difficultyCount; i++) {
				var part = (Instrument) reader.ReadInt32();
				int difficulty = reader.ReadInt32();
				PartDifficulties.Add(part, difficulty);
			}

			BandDifficulty = reader.ReadInt32();
			AvailableParts = (ulong) reader.ReadInt64();
			VocalParts = reader.ReadInt32();
			CacheRoot = folder;

			// Some songs may include their full date within the Year field. This code removes the month and day from those fields.
			if (Year.Length > 4) {
				int yearFirstIndex = 0;
				int contiguousNumCount = 0;
				for (int i = 0; i < Year.Length; i++) {
					if (contiguousNumCount >= 4) {
						break;
					}

					if (char.IsDigit(Year[i])) {
						contiguousNumCount++;
					} else {
						contiguousNumCount = 0;
						yearFirstIndex = i + 1;
					}
				}
				int yearLastIndex = yearFirstIndex + contiguousNumCount;
				Year = Year[yearFirstIndex..yearLastIndex];
			}
		}

		public void ReadCacheEnding(BinaryReader reader) {
			Checksum = reader.ReadString();
			NotesFile = reader.ReadString();
			Location = reader.ReadString();
		}

		public virtual void WriteMetadataToCache(BinaryWriter writer) {
			writer.Write((int) DrumType);

			writer.Write(Name);
			writer.Write(Artist);
			writer.Write(Charter);
			writer.Write(IsMaster);
			writer.Write(Album);
			writer.Write(AlbumTrack);
			writer.Write(PlaylistTrack);
			writer.Write(Genre);
			writer.Write(Year);
			writer.Write(SongLength);
			writer.Write(PreviewStart);
			writer.Write(PreviewEnd);
			writer.Write(Delay);
			writer.Write(LoadingPhrase);
			writer.Write(HopoThreshold);
			writer.Write(EighthNoteHopo);
			writer.Write(MultiplierNote);
			writer.Write(Source);

			// Write difficulties
			writer.Write(PartDifficulties.Count);
			foreach (var difficulty in PartDifficulties) {
				writer.Write((int) difficulty.Key);
				writer.Write(difficulty.Value);
			}

			writer.Write(BandDifficulty);
			writer.Write(AvailableParts);
			writer.Write(VocalParts);
		}

		public void WriteCacheEnding(BinaryWriter writer) {
			writer.Write(Checksum);
			writer.Write(NotesFile);
			writer.Write(Location);
		}

		public bool HasInstrument(Instrument instrument) {
			// FL is my favourite hexadecimal number
			long instrumentBits = 0xFL << (int) instrument * 4;
			return (AvailableParts & (ulong) instrumentBits) != 0;
		}

		public bool HasPart(Instrument instrument, Difficulty difficulty) {
			long instrumentBits = 0x1L << (int)instrument * 4 + (int)difficulty;
			return (AvailableParts & (ulong) instrumentBits) != 0;
		}

		
	}
}