using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text;
using UnityEngine;
using YARG.Data;
using DtxCS;
using DtxCS.DataTypes;

public class SongData
{
	// all the possible metadata you could possibly want from a particular songs.dta
	private string shortname, name, artist, songPath, gameOrigin, genre, albumName;
	private uint songId, songLength;
	private short tuningOffsetCents;
	private ushort yearReleased, yearRecorded, vocalTonicNote;
	// since shorts and bools aren't nullable, use hasWhatever to determine whether or not to include them in-game
	private bool master, albumArt, hasVocalTonicNote, hasSongTonality, songTonality, vocalGender; //vocalGender is true if male, false if female
	private byte vocalParts, rating, albumTrackNumber;
	private (uint start, uint end) preview;
	private float[] pans, vols;
	private short[] cores, realGuitarTuning, realBassTuning;
	private Dictionary<string, byte[]> tracks;
	private Dictionary<string, ushort> ranks;

	private static DataAtom GetDataAtom(DataArray dta, string key){ return (dta.Array(key) == null) ? null : (DataAtom)dta.Array(key)[1]; }
	private static DataArray GetNestedDataArray(DataArray dta, string key){ return (dta.Array(key) == null) ? null : (DataArray)dta.Array(key)[1]; }
	private static DataSymbol GetDataSymbol(DataArray dta, string key){ return (DataSymbol)dta.Array(key)[1]; }
	private static DataArray GetDataDict(DataArray dta, string key){ return dta.Array(key); }

	private static float[] CreateFloatArray(DataArray dta){
		if(dta == null) return null;
		float[] res = new float[dta.Count];
		for(int i = 0; i < dta.Count; i++) res[i] = ((DataAtom)dta[i]).Float;
		return res;
	}

	private static short[] CreateShortArray(DataArray dta){
		if(dta == null) return null;
		short[] res = new short[dta.Count];
		for(int i = 0; i < dta.Count; i++) res[i] = (short)((DataAtom)dta[i]).Int;
		return res;
	}

	public SongData ParseFromDataArray(DataArray dta){
		// parse outermost dta non-array values first
		// TODO: account for when the following dta attributes could be missing:
		// song_id, song_length, game_origin, rating, album_art, album_name, album_track_number, year_released
		shortname = dta.Name;
		name = GetDataAtom(dta, "name").String;
		artist = GetDataAtom(dta, "artist").String;
		string master_str = dta.Array("master")[1].ToString();
		master = (master_str.ToUpper() == "TRUE" || master_str == "1");
		songId = (uint)GetDataAtom(dta, "song_id").Int;
		songLength = (uint)GetDataAtom(dta, "song_length").Int;
		preview = ((uint)((DataAtom)dta.Array("preview")[1]).Int, (uint)((DataAtom)dta.Array("preview")[2]).Int);
		gameOrigin = GetDataSymbol(dta, "game_origin").Name;
		genre = GetDataSymbol(dta, "genre").Name;
		rating = (byte)GetDataAtom(dta, "rating").Int;
		vocalGender = (GetDataSymbol(dta, "vocal_gender").Name == "male");
		string album_art_str = dta.Array("album_art")[1].ToString();
		albumArt = (album_art_str.ToUpper() == "TRUE" || album_art_str == "1");
		albumName = GetDataAtom(dta, "album_name").String;
		albumTrackNumber = (byte)GetDataAtom(dta, "album_track_number").Int;
		yearReleased = (ushort)GetDataAtom(dta, "year_released").Int;

		//get instrument ranks
		DataArray rankArray = GetDataDict(dta, "rank");
		ranks = new Dictionary<string, ushort>();
		for(int j = 0; j < rankArray.Count; j++)
			if(rankArray[j] is DataArray inner)
				ranks.Add(((DataSymbol)inner[0]).Name, (ushort)((DataAtom)inner[1]).Int);

		// get (song ) attributes (name, tracks, pans/vols/cores, etc)
		DataArray songArray = GetDataDict(dta, "song");
		songPath = GetDataSymbol(songArray, "name").Name;
		pans = CreateFloatArray(GetNestedDataArray(songArray, "pans"));
		vols = CreateFloatArray(GetNestedDataArray(songArray, "vols"));
		cores = CreateShortArray(GetNestedDataArray(songArray, "cores"));

		DataArray trackArray = GetDataDict(GetDataDict(songArray, "tracks"), ""); 
		tracks = new Dictionary<string, byte[]>();
		for(int b = 0; b < trackArray.Count; b++){
			if(trackArray[b] is DataArray inner){
				string key = "";
				byte[] val = null;
				for(int c = 0; c < inner.Count; c++){
					if(inner[c] is DataSymbol innerSymbol)
						key = innerSymbol.Name;
					else if(inner[c] is DataArray innerInner){
						val = new byte[innerInner.Count];
						for(int d = 0; d < innerInner.Count; d++)
							if(innerInner[d] is DataAtom innerInnerAtom)
								val[d] = (byte)innerInnerAtom.Int;
					}
					else if(inner[c] is DataAtom innerAtom)
						val = new byte[] { (byte)innerAtom.Int };
				}
				if(val.Length > 0) tracks.Add(key, val);
			}
		}

		// vocal parts
		if(!ranks.ContainsKey("vocals") || ranks["vocals"] == 0) vocalParts = 0;
		else{
			DataAtom vocPartsAtom = GetDataAtom(songArray, "vocal_parts");
			vocalParts = (vocPartsAtom != null) ? (byte)vocPartsAtom.Int : (byte)1;
		}

		// year_recorded, real_guitar_tuning, real_bass_tuning, vocal_tonic_note, song_tonality, and tuning_offset_cents
		// ARE currently accounted for regarding possibly being missing
		DataAtom yearRecordedAtom = GetDataAtom(dta, "year_recorded");
		yearRecorded = (yearRecordedAtom != null) ? (ushort)yearRecordedAtom.Int : yearReleased;

		DataAtom vocalTonicNoteAtom = GetDataAtom(dta, "vocal_tonic_note");
		hasVocalTonicNote = (vocalTonicNoteAtom != null);
		if(hasVocalTonicNote) vocalTonicNote = (ushort)vocalTonicNoteAtom.Int;

		DataAtom songTonalityAtom = GetDataAtom(dta, "song_tonality");
		hasSongTonality = (songTonalityAtom != null);
		if(hasSongTonality) songTonality = (songTonalityAtom.Int == 1);

		DataAtom tuningOffsetAtom = GetDataAtom(dta, "tuning_offset_cents");
		tuningOffsetCents = (tuningOffsetAtom != null) ? (short)tuningOffsetAtom.Int : (short)0;
		
		realGuitarTuning = CreateShortArray(GetNestedDataArray(dta, "real_guitar_tuning"));
		realBassTuning = CreateShortArray(GetNestedDataArray(dta, "real_bass_tuning"));

		return this;
	}

	public override string ToString(){
		string debugTrackStr = "";
		foreach(var kvp in tracks) debugTrackStr += $"{kvp.Key}, ({string.Join(", ", kvp.Value)}) ";

		return string.Join(Environment.NewLine,
			$"song id={songId}; shortname={shortname}: name={name}; artist={((!master) ? "as made famous by " : "")}{artist}",
			$"song path={songPath}; vocal parts={vocalParts}; vocal gender={((vocalGender) ? "male" : "female")}",
			$"pans=({string.Join(", ", pans)})",
			$"vols=({string.Join(", ", vols)})",
			$"cores=({string.Join(", ", cores)})",
			$"tracks={string.Join(", ", debugTrackStr)}",
			$"ranks={string.Join(", ", ranks)}",
			$"album art={albumArt}; album name={albumName}; album track number={albumTrackNumber}",
			$"year released={yearReleased}; year recorded={yearRecorded}",
			$"song length={songLength}; preview={preview}; game origin={gameOrigin}; genre={genre}; rating={rating}",
			$"vocal tonic note={hasVocalTonicNote} {((hasVocalTonicNote) ? (vocalTonicNote) : "")}",
			$"song tonality={hasSongTonality} {((hasSongTonality) ? (songTonality) : "")}",
			$"tuning offset cents={tuningOffsetCents}",
			$"real guitar tuning=({((realGuitarTuning != null) ? string.Join(", ", realGuitarTuning) : "")})",
			$"real bass tuning=({((realBassTuning != null) ? string.Join(", ", realBassTuning) : "")})"
		);
	}

}

namespace YARG.Serialization {
	public static class RockBandSTFS {
		static RockBandSTFS() {}
		public static List<SongInfo> ParseSongsDta(DirectoryInfo srcfolder) {
			try {
				List<SongInfo> songList = new List<SongInfo>();
				Encoding dtaEnc = Encoding.GetEncoding("iso-8859-1"); // "dtxcs reads things properly" so turns out that's a lie perpetuated by big c#
				DataArray dtaTree = new DataArray();
				using (StreamReader temp = new StreamReader(Path.Combine(srcfolder.FullName, "songs.dta"), dtaEnc)) {
					dtaTree = DTX.FromDtaString(temp.ReadToEnd());
				}		

				// parse songs.dta for all the songs and their info
				List<SongData> parsedSongs = new List<SongData>();
				for(int i = 0; i < dtaTree.Count; i++){
					SongData currentSong = new SongData();
					parsedSongs.Add(currentSong.ParseFromDataArray((DataArray)dtaTree[i]));
				}
				
				// print out each SongData's, well, song data - useful for debugging
				for(int j = 0; j < parsedSongs.Count; j++)
					Debug.Log(parsedSongs[j].ToString());

				string songPath = "songs/test/test";
				string songPathGen = "songs/" + songPath.Split("/")[1] + "/gen/" + songPath.Split("/")[2];
				// Mackiloha.IO.SystemInfo sysInfo = new Mackiloha.IO.SystemInfo {
				// 	Version = 25, 
				// 	Platform = Platform.X360, 
				// 	BigEndian = true
				// };
				// var bitmap = new MiloSerializer(sysInfo).ReadFromFile<HMXBitmap>(Path.Combine(Path.Combine(srcfolder.FullName, songPathGen), "_keep.png_xbox"));
				// string tmpFilePath = Path.GetTempFileName() + ".png";
           		// bitmap.SaveAs(sysInfo, tmpFilePath);
				return songList;
			} catch (Exception e) {
				Debug.LogError($"Failed to parse songs.dta for `{srcfolder.FullName}`.");
				Debug.LogException(e);
				return null;
			}
		}
	}
}