using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DtxCS;
using DtxCS.DataTypes;
using UnityEngine;
using XboxSTFS;
using YARG.Data;
using YARG.Song;

namespace YARG.Serialization {
	public static class XboxSongUpgradeBrowser {
        public static Dictionary<SongProUpgrade, DataArray> FetchSongUpgrades(string upgrade_folder){
            var dtaTree = new DataArray();
			var UpgradeSongDict = new Dictionary<SongProUpgrade, DataArray>();

			// TODO: tweak this function so you parse raw upgrades, and THEN upgrades contained within CONs
			// FIRST, parse raw upgrades - start by attempting to read upgrades.dta
			if(File.Exists(Path.Combine(upgrade_folder, "upgrades.dta"))){
				using var sr = new StreamReader(Path.Combine(upgrade_folder, "upgrades.dta"), Encoding.GetEncoding("iso-8859-1"));
				dtaTree = DTX.FromDtaString(sr.ReadToEnd());

				// Read each shortname the dta file lists
				for (int i = 0; i < dtaTree.Count; i++) {
					try {
						var currentArray = (DataArray) dtaTree[i];
						var upgr = new SongProUpgrade();
						upgr.ShortName = currentArray.Name;
						upgr.UpgradeMidiPath = Path.Combine(upgrade_folder, $"{currentArray.Name}_plus.mid");
						UpgradeSongDict.Add(upgr, currentArray);
					} catch (Exception e) {
						Debug.Log($"Failed to get upgrade, skipping...");
						Debug.LogException(e);
					}
				}
			}

			// THEN, find any loose CONs in this directory and parse those for upgrades as well
			foreach (var file in Directory.EnumerateFiles(upgrade_folder)) {
				if(Path.GetExtension(file) != ".mid" && Path.GetExtension(file) != ".dta"){
					// for each file found, read first 4 bytes and check for "CON " or "LIVE"
					using var fs = new FileStream(file, FileMode.Open, FileAccess.Read);
					using var br = new BinaryReader(fs);
					string fHeader = Encoding.UTF8.GetString(br.ReadBytes(4));
					if (fHeader == "CON " || fHeader == "LIVE") {
						var thisUpgradeCONFileListings = XboxSTFSParser.GetCONFileListings(file);

						// attempt to read the CON's upgrades.dta
						if(thisUpgradeCONFileListings.TryGetValue(Path.Combine("songs_upgrades", "upgrades.dta"), out var UpgradeFL)){
							try {
								dtaTree = DTX.FromPlainTextBytes(XboxSTFSParser.GetFile(file, UpgradeFL));
							} catch (Exception e) {
								Debug.LogError($"Failed to parse upgrades.dta for `{file}`.");
								Debug.LogException(e);
								continue;
							}
						}

						// Read each shortname the dta file lists
						for(int i = 0; i < dtaTree.Count; i++){
							try {
								var currentArray = (DataArray) dtaTree[i];
								var upgr = new SongProUpgrade();
								upgr.ShortName = currentArray.Name;
								upgr.UpgradeMidiPath = Path.Combine("songs_upgrades", $"{currentArray.Name}_plus.mid");
								upgr.CONFilePath = file;
								upgr.UpgradeFL = UpgradeFL;
								UpgradeSongDict.Add(upgr, currentArray);
							} catch (Exception e) {
								Debug.Log($"Failed to get upgrade, skipping...");
								Debug.LogException(e);
							}
						}

					}
				}
			}

            // Debug.Log($"Song upgrades:");
			// foreach(var item in UpgradeSongDict){
			// 	Debug.Log($"{item.Key.ShortName} has a pro upgrade");
			// }

            return UpgradeSongDict;

        }
    }
}