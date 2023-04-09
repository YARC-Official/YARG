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
	// songid, shortname, game origin, name, artist, album, year, origyear, track, genre, song length, preview, rank, album art, master, vocal parts, gender
	private string shortname;
    private string name;
    private string artist;
	private bool master;
	// song info
	private string songPath;
	private float[] pans;
	private float[] vols;
	private short[] cores;
	// mogg channel tracks
	private Dictionary<string, byte[]> tracks;
	private byte vocalParts;
	private uint songId;
    private uint songLength;
    private uint[] preview;
	private Dictionary<string, ushort> ranks;
	private bool vocalGender; //true if male, false if female
	private string gameOrigin;
    private string genre;
    private string albumName;
	private bool albumArt;
	private byte rating;
	private byte albumTrackNumber;
    private ushort yearReleased;
	private ushort yearRecorded;
	private short[] realGuitarTuning;
	private short[] realBassTuning;

	public SongData ParseFromDataArray(DataArray dta){
		shortname = dta.Name;
		name = dta.Array("name")[1].ToString();
		artist = dta.Array("artist")[1].ToString();
		string master_str = dta.Array("master")[1].ToString();
		master = (master_str.ToUpper() == "TRUE" || master_str == "1");
		songId = UInt32.Parse(dta.Array("song_id")[1].ToString());
		songLength = UInt32.Parse(dta.Array("song_length")[1].ToString());
		preview = new uint[2] {UInt32.Parse(dta.Array("preview")[1].ToString()), UInt32.Parse(dta.Array("preview")[2].ToString())};
		gameOrigin = dta.Array("game_origin")[1].ToString();
		genre = dta.Array("genre")[1].ToString();
		rating = Byte.Parse(dta.Array("rating")[1].ToString()); 
		string album_art_str = dta.Array("album_art")[1].ToString();
		albumArt = (album_art_str.ToUpper() == "TRUE" || album_art_str == "1");
		albumName = dta.Array("album_name")[1].ToString();
		albumTrackNumber = Byte.Parse(dta.Array("album_track_number")[1].ToString());
		yearReleased = UInt16.Parse(dta.Array("year_released")[1].ToString());
		yearRecorded = (dta.Array("year_recorded") != null) ? UInt16.Parse(dta.Array("year_recorded")[1].ToString()) : yearReleased;

		songPath = dta.Array("song").Array("name")[1].ToString();
		vocalParts = (dta.Array("song").Array("vocal_parts") != null) ? Byte.Parse(dta.Array("song").Array("vocal_parts")[1].ToString()) : (byte)1;
		
		pans = Array.ConvertAll(dta.Array("song").Array("pans")[1].ToString()[1..^1].Split(' '), float.Parse);
		vols = Array.ConvertAll(dta.Array("song").Array("vols")[1].ToString()[1..^1].Split(' '), float.Parse);
		cores = Array.ConvertAll(dta.Array("song").Array("cores")[1].ToString()[1..^1].Split(' '), short.Parse);

		vocalGender = (dta.Array("vocal_gender")[1].ToString() == "male");

		//mogg tracks
		DataArray trackArray = dta.Array("song").Array("tracks").Array("");
		tracks = new Dictionary<string, byte[]>();
		for(int a = 0; a < trackArray.Count; a++){
			Debug.Log(trackArray[a].ToString());
			string instr_key = trackArray[a].ToString().Split(' ')[0].Substring(1);
			Debug.Log($"w instr_key: {trackArray.Array(instr_key)[1].ToString()}");
			tracks.Add(instr_key, Array.ConvertAll(trackArray.Array(instr_key)[1].ToString().Replace("(","").Replace(")","").Split(' '), byte.Parse));
		}

		//instrument ranks
		DataArray rankArray = dta.Array("rank");
		ranks = new Dictionary<string, ushort>();
		for(int i = 1; i < rankArray.Count; i++){
			string instr_key = rankArray[i].ToString().Split(' ')[0].Substring(1);
			ranks.Add(instr_key, UInt16.Parse(rankArray.Array(instr_key)[1].ToString()));
		}

		if(!ranks.ContainsKey("vocals") || ranks["vocals"] == 0) vocalParts = 0;

		//real guitar and bass tunings
		if(dta.Array("real_guitar_tuning") != null){
			Debug.Log(dta.Array("real_guitar_tuning")[1].ToString());
			realGuitarTuning = Array.ConvertAll(dta.Array("real_guitar_tuning")[1].ToString()[1..^1].Split(' '), short.Parse);
		}
		if(dta.Array("real_bass_tuning") != null){
			Debug.Log(dta.Array("real_bass_tuning")[1].ToString());
			realBassTuning = Array.ConvertAll(dta.Array("real_bass_tuning")[1].ToString()[1..^1].Split(' '), short.Parse);
		}

		// DataArray songArray = new DataArray();
		// songArray = dta.Array("song");
		// for(int i = 0; i < songArray.Count; i++){
		// 	Debug.Log($"idx {i} type {songArray[i].GetType()} = {songArray[i].ToString()}");
		// 	if(songArray[i].GetType() == typeof(DataArray)){
		// 		DataArray innerArray = new DataArray();
		// 		string arrayName = songArray[i].ToString().Split(' ')[0].Substring(1);
		// 		innerArray = songArray.Array(arrayName);
		// 		for(int j = 0; j < innerArray.Count; j++){
		// 			Debug.Log($"jdx {j} type {innerArray[j].GetType()} = {innerArray[j].ToString()}");
		// 		}
		// 		// Debug.Log($"the array name: {songArray[i].ToString().Split(' ')[0].Substring(1)}");
		// 	}
		// }

		return this;
	}

	public override string ToString(){
		List<string> debugTrackArray = new List<string>();
		foreach(var kvp in tracks){
			debugTrackArray.Add($"{kvp.Key}, {string.Join(", ", kvp.Value)}");
		}

		return string.Join(Environment.NewLine,
			$"song id={songId}; shortname={shortname}: name={name}; artist={((!master) ? "as made famous by " : "")}{artist};",
			$"song path={songPath}; vocal parts={vocalParts}; vocal gender={((vocalGender) ? "male" : "female")};",
			$"pans=({string.Join(", ", pans)});",
			$"vols=({string.Join(", ", vols)});",
			$"cores=({string.Join(", ", cores)});",
			$"tracks={string.Join(", ", tracks)}",
			$"ranks={string.Join(", ", ranks)}",
			$"album art={albumArt}; album name={albumName}; album track number={albumTrackNumber};",
			$"year released={yearReleased}; year recorded={yearRecorded}",
			$"song length={songLength}; preview=({preview[0]}, {preview[1]}); game origin={gameOrigin}; genre={genre}; rating={rating};",
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