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
        public static List<(SongProUpgrade, DataArray)> FetchSongUpgrades(string upgrade_folder, ref List<XboxSTFSFile> conFiles){
			DataArray dtaTree;
			var songUpgrades = new List<(SongProUpgrade, DataArray)>();

			// TODO: tweak this function so you parse raw upgrades, and THEN upgrades contained within CONs
			// FIRST, parse raw upgrades - start by attempting to read upgrades.dta
			if(File.Exists(Path.Combine(upgrade_folder, "upgrades.dta"))){
				dtaTree = DTX.FromPlainTextBytes(File.ReadAllBytes(Path.Combine(upgrade_folder, "upgrades.dta")));

				// Read each shortname the dta file lists
				for (int i = 0; i < dtaTree.Count; i++) {
					try {
						songUpgrades.Add(new(new SongProUpgrade(upgrade_folder, dtaTree[i].Name), (DataArray) dtaTree[i]));
					} catch (Exception e) {
						Debug.Log($"Failed to get upgrade, skipping...");
						Debug.LogException(e);
					}
				}
			}

			// THEN, find any loose CONs in this directory and parse those for upgrades as well
			foreach (var file in Directory.EnumerateFiles(upgrade_folder)) {
				if(Path.GetExtension(file) != ".mid" && Path.GetExtension(file) != ".dta"){
					XboxSTFSFile conFile = XboxSTFSFile.LoadCON(file);
					if (conFile == null) { continue; }

					byte[] upgradeFile = conFile.LoadSubFile(Path.Combine("song_upgrades","upgrades.dta"));
					if (upgradeFile.Length == 0) { continue; }

					try {
						dtaTree = DTX.FromPlainTextBytes(upgradeFile);
					} catch (Exception e) {
						Debug.LogError($"Failed to parse upgrades.dta for `{file}`.");
						Debug.LogException(e);
						continue;
					}

					conFiles.Add(conFile);

					for (int i = 0; i < dtaTree.Count; i++) {
						try {
							songUpgrades.Add(new(new SongProUpgrade(conFile, dtaTree[i].Name), (DataArray) dtaTree[i]));
						} catch (Exception e) {
							Debug.Log($"Failed to get upgrade, skipping...");
							Debug.LogException(e);
						}
					}
					
				}
			}
            return songUpgrades;

        }
    }
}