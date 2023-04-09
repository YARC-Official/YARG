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
	private string shortname;
    private string name;
    private string artist;
	private bool master;
    // public string songPath;
    // public int songLength;
    // public int[] preview;
    // public Dictionary<string, int> rank;
    private string genre;
    private string albumName;
	private int albumTrackNumber;
    private int yearReleased;

	public SongData ParseFromDataArray(DataArray dta){
		shortname = dta.Name;
		name = dta.Array("name")[1].ToString();
		artist = dta.Array("artist")[1].ToString();
		genre = dta.Array("genre")[1].ToString();
		albumName = dta.Array("album_name")[1].ToString();
		albumTrackNumber = Int16.Parse(dta.Array("album_track_number")[1].ToString());
		yearReleased = Int16.Parse(dta.Array("year_released")[1].ToString());
		return this;
	}

	public override string ToString(){
		return $"{shortname}: name={name}; artist={artist}; genre={genre}; album name={albumName}; album track number={albumTrackNumber}; year released={yearReleased}";
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