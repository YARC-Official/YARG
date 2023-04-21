using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using UnityEngine;
using YARG.Data;
using DtxCS;
using DtxCS.DataTypes;
using YARG.Serialization;

// using YARG.PlayMode;
// using ManagedBass;
// using ManagedBass.DirectX8;
// using ManagedBass.Fx;
// using ManagedBass.Mix;

namespace YARG.Serialization {
	public static class XboxRawfileBrowser {
		public static List<XboxSong> BrowseFolder(DirectoryInfo folder){
			Debug.Log($"XboxRawFileBrowser parsing base folder {folder}");
			List<XboxSong> songList = new List<XboxSong>();
			DataArray dtaTree = new DataArray();
			// attempt to read songs.dta
			try {
				using(StreamReader sr = new StreamReader(Path.Combine(folder.FullName, "songs.dta"), Encoding.GetEncoding("iso-8859-1")))
					dtaTree = DTX.FromDtaString(sr.ReadToEnd());
				Debug.Log($"sweet, successfully read dta");
			}
			catch(Exception e){
				Debug.LogError($"Failed to parse songs.dta for `{folder.FullName}`.");
				Debug.LogException(e);
				return null;
			}

			//for each song the dta found
			for(int i = 0; i < dtaTree.Count; i++){
				try{
					DataArray currentArray = (DataArray)dtaTree[i];
					XboxSong currentSong = new XboxSong(folder.ToString(), currentArray);

					if(currentSong.ValidateSong()){
						songList.Add(currentSong);
					}
				}
				catch(Exception e){
					Debug.Log($"Failed to load song, skipping...");
					Debug.LogException(e);
				}
			}

			return songList;
		}
	}
// 	public static class RockBandSTFS {
// 		static RockBandSTFS() {}
// 		// passes in root folder (where songs.dta and all the relevant song folders would be)
// 		// first, parse songs.dta
// 		// for each song you find:
// 		// if (fake TRUE), disregard and continue
// 		// else, check mogg for 0xA header
// 		// if the two look legit, then create an XboxSong
// 		public static List<SongInfo> ParseSongsDta(DirectoryInfo srcfolder) {
// 			try {
// 				List<SongInfo> songList = new List<SongInfo>();
// 				Encoding dtaEnc = Encoding.GetEncoding("iso-8859-1"); // "dtxcs reads things properly" so turns out that's a lie perpetuated by big c#
// 				DataArray dtaTree = new DataArray();
// 				using (StreamReader temp = new StreamReader(Path.Combine(srcfolder.FullName, "songs.dta"), dtaEnc)) {
// 					dtaTree = DTX.FromDtaString(temp.ReadToEnd());
// 				}		

// 				// parse songs.dta for all the songs and their info
// 				List<XboxSongData> parsedSongs = new List<XboxSongData>();
// 				for(int i = 0; i < dtaTree.Count; i++){
// 					XboxSongData currentSong = new XboxSongData((DataArray)dtaTree[i]);
// 					parsedSongs.Add(currentSong);
// 					// string testPng = srcfolder.ToString() + $"/{currentSong.GetShortName()}/gen/{currentSong.GetShortName()}_keep.png_xbox";
// 					// XboxImage art = new XboxImage(testPng);
// 					// art.ParseImage();
// 					// art.SaveImageToDisk($"{currentSong.GetShortName()}_lol");
// 				}
				
// 				// print out each XboxSongData's, well, song data - useful for debugging
// 				for(int j = 0; j < parsedSongs.Count; j++)
// 					Debug.Log(parsedSongs[j].ToString());

// 				// testing mogg parsing
// 				string testMogg = srcfolder.ToString() + "/giveitaway2/giveitaway2.mogg";

// 				if(File.Exists(Path.Combine(srcfolder.FullName, testMogg))){
// 					Debug.Log("neato, mogg exists");
// 					byte[] buffer = new byte[4];
// 					int startAddress;
// 					long moggLength;
// 					using(FileStream fs = new FileStream(testMogg, FileMode.Open, FileAccess.Read)){
//                 		using(BinaryReader br = new BinaryReader(fs, new ASCIIEncoding())){
// 							buffer = br.ReadBytes(4);
// 							buffer = br.ReadBytes(4);
// 							startAddress = BitConverter.ToInt32(buffer,0);
// 							Debug.Log($"ogg audio begins at memory address {startAddress:X}");
// 							Debug.Log($"# of ogg bytes: {fs.Length}");
// 							moggLength = fs.Length;
// 							// byte[] audioBuffer = new byte[fs.Length - startAddress];
// 						}
						
// 					}

// 					// var moggData = File.ReadAllBytes(testMogg)[startAddress..];
// 					// int streamHandle = Bass.CreateStream(moggData, 0, moggData.Length, BassFlags.Prescan | BassFlags.Decode | BassFlags.AsyncFile);
// 					int streamHandle = Bass.CreateStream(testMogg, startAddress, (moggLength - startAddress), BassFlags.Prescan | BassFlags.Decode | BassFlags.AsyncFile);
// 					if(streamHandle == 0) Debug.Log($"failed to create stream: {Bass.LastError}");

// 					int[] splits = new int[14];
// 					for(int i = 0; i < splits.Length; i++){
// 						splits[i] = BassMix.CreateSplitStream(streamHandle, BassFlags.Decode, new[] {i, -1});
// 						if(splits[i] == 0) Debug.Log($"failed to create split stream: {Bass.LastError}");
// 					}

// 					int mixerHandle = BassMix.CreateMixerStream(44100, 2, BassFlags.Default);
// 					if(mixerHandle == 0) Debug.Log($"failed to create mixer: {Bass.LastError}");

// 					var channelVolumes = new[,] { {0.5f}, {0.5f} };

// 					for(var index = 0; index < splits.Length; index++){
// 						int channel = splits[index];
// 						BassMix.MixerAddChannel(mixerHandle, channel, BassFlags.MixerChanMatrix);

// 						//set matrix on individual channels
// 						bool setMatrixToChannel = BassMix.ChannelSetMatrix(channel, channelVolumes);
// 						if(!setMatrixToChannel) Debug.Log($"failed to set matrix to channel {index}: {Bass.LastError}");
// 					}

// 					bool playSong = Bass.ChannelPlay(mixerHandle);
// 					if(!playSong) Debug.Log($"failed to play song: {Bass.LastError}");

// 				}
// 				else Debug.Log("kowabummer");

// 				// testing png_xbox parsing
// 				// string testPng = srcfolder.ToString() + "/underthebridge/gen/underthebridge_keep.png_xbox";
// 				// if(File.Exists(Path.Combine(srcfolder.FullName, testPng))){
// 				// 	Debug.Log("png exists");
// 				// 	XboxImage art = new XboxImage(testPng);
// 				// 	art.ParseImage();
// 				// 	art.SaveImageToDisk("lol");
// 				// }
// 				// else Debug.Log("nah dawg");

// 				// string testPng3 = srcfolder.ToString() + "/underthebridge/gen/soybomb.bmp_xbox";
// 				// XboxImage art3 = new XboxImage(testPng3);
// 				// art3.ParseImage();
// 				// art3.SaveImageToDisk("lmao");

// 				// string othertest = srcfolder.ToString() + "/underthebridge/gen/hotforteacher_keep.png_xbox";
// 				// XboxImage otherart = new XboxImage(othertest);
// 				// otherart.ParseImage();
// 				// otherart.SaveImageToDisk("asdf");
// 				// for (int k = 0; k < parsedSongs.Count; k++) {
// 				// 	SongInfo currSong = new SongInfo(new DirectoryInfo(Path.Combine(srcfolder.FullName, parsedSongs[k].GetShortName())), parsedSongs[k].GetShortName());
// 				// 	currSong.SongNameWithFlags = parsedSongs[k].GetSongName();
// 				// 	currSong.artistName = parsedSongs[k].GetArtist();
// 				// 	currSong.isSongIni = false;
// 				// 	songList.Add(currSong);
// 				// }
// 				return songList;
// 			} catch (Exception e) {
// 				Debug.LogError($"Failed to parse songs.dta for `{srcfolder.FullName}`.");
// 				Debug.LogException(e);
// 				return null;
// 			}
// 		}
// 	}
}