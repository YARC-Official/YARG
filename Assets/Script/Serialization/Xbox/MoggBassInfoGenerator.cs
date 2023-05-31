using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DtxCS;
using DtxCS.DataTypes;
using UnityEngine;
using YARG.Data;
using YARG.Song;

namespace YARG.Serialization {
	public static class MoggBASSInfoGenerator {
        public static void Generate(ExtractedConSongEntry song, DataArray dta, List<DataArray> dta_update_roots = null){
            var Tracks = new Dictionary<string, int[]>();
			float[] PanData = null, VolumeData = null;
			int[] CrowdChannels = null;
			int ChannelCount = 0;
			DataArray dta_update;
			List<DataArray> dtas_to_parse = new(){ dta };

			// determine whether or not we even NEED to parse the update dta for mogg information
			if(dta_update_roots != null){
				foreach(var dta_update_root in dta_update_roots){
					dta_update = dta_update_root.Array("song");
					if(dta_update != null){
						if(dta_update.Array("tracks") != null || dta_update.Array("pans") != null || 
							dta_update.Array("vols") != null || dta_update.Array("crowd_channels") != null)
							dtas_to_parse.Add(dta_update);
					}
				}
			}
			
			foreach(var dta_to_parse in dtas_to_parse){
				for (int i = 1; i < dta_to_parse.Count; i++) {
					var dtaArray = (DataArray) dta_to_parse[i];
					switch (dtaArray[0].ToString()) {
						case "tracks":
							Tracks.Clear();
							var trackArray = (DataArray) dtaArray[1];
							for (int x = 0; x < trackArray.Count; x++) {
								if (trackArray[x] is not DataArray instrArray) continue;
								string key = ((DataSymbol) instrArray[0]).Name;
								int[] val;
								if (instrArray[1] is DataArray trackNums) {
									if (trackNums.Count <= 0) continue;
									val = new int[trackNums.Count];
									for (int y = 0; y < trackNums.Count; y++)
										val[y] = ((DataAtom) trackNums[y]).Int;
									Tracks.Add(key, val);
								} else if (instrArray[1] is DataAtom trackNum) {
									val = new int[1];
									val[0] = trackNum.Int;
									Tracks.Add(key, val);
								}
							}
							break;
						case "pans":
							var panArray = dtaArray[1] as DataArray;
							PanData = new float[panArray.Count];
							for (int p = 0; p < panArray.Count; p++) PanData[p] = ((DataAtom) panArray[p]).Float;
							ChannelCount = panArray.Count;
							break;
						case "vols":
							var volArray = dtaArray[1] as DataArray;
							VolumeData = new float[volArray.Count];
							for (int v = 0; v < volArray.Count; v++){
								var volAtom = (DataAtom) volArray[v];
								if(volAtom.Type == DataType.FLOAT) VolumeData[v] = ((DataAtom) volArray[v]).Float;
								else VolumeData[v] = ((DataAtom) volArray[v]).Int;
							}
							break;
						case "crowd_channels":
							CrowdChannels = new int[dtaArray.Count - 1];
							for (int cc = 1; cc < dtaArray.Count; cc++)
								CrowdChannels[cc - 1] = ((DataAtom) dtaArray[cc]).Int;
							break;
					}
				}
			}

			// now that we have all the info we need from dta, calculate BASS info
			var mapped = new bool[ChannelCount];

			// BEGIN BASS Stem Mapping ----------------------------------------------------------------------

			if (Tracks.TryGetValue("drum", out var drumArray)) {
				switch (drumArray.Length) {
					//drum (0 1): stereo kit --> (0 1)
					case 2:
						song.StemMaps[SongStem.Drums] = new[] { drumArray[0], drumArray[1] };
						break;
					//drum (0 1 2): mono kick, stereo snare/kit --> (0) (1 2)
					case 3:
						song.StemMaps[SongStem.Drums1] = new[] { drumArray[0] };
						song.StemMaps[SongStem.Drums2] = new[] { drumArray[1], drumArray[2] };
						break;
					//drum (0 1 2 3): mono kick, mono snare, stereo kit --> (0) (1) (2 3)
					case 4:
						song.StemMaps[SongStem.Drums1] = new[] { drumArray[0] };
						song.StemMaps[SongStem.Drums2] = new[] { drumArray[1] };
						song.StemMaps[SongStem.Drums3] = new[] { drumArray[2], drumArray[3] };
						break;
					//drum (0 1 2 3 4): mono kick, stereo snare, stereo kit --> (0) (1 2) (3 4)
					case 5:
						song.StemMaps[SongStem.Drums1] = new[] { drumArray[0] };
						song.StemMaps[SongStem.Drums2] = new[] { drumArray[1], drumArray[2] };
						song.StemMaps[SongStem.Drums3] = new[] { drumArray[3], drumArray[4] };
						break;
					//drum (0 1 2 3 4 5): stereo kick, stereo snare, stereo kit --> (0 1) (2 3) (4 5)
					case 6:
						song.StemMaps[SongStem.Drums1] = new[] { drumArray[0], drumArray[1] };
						song.StemMaps[SongStem.Drums2] = new[] { drumArray[2], drumArray[3] };
						song.StemMaps[SongStem.Drums3] = new[] { drumArray[4], drumArray[5] };
						break;
				}

				foreach (int arr in drumArray) {
					mapped[arr] = true;
				}
			}

			if (Tracks.TryGetValue("bass", out var bassArray)) {
				song.StemMaps[SongStem.Bass] = new int[bassArray.Length];
				for (int i = 0; i < bassArray.Length; i++) {
					song.StemMaps[SongStem.Bass][i] = bassArray[i];
					mapped[bassArray[i]] = true;
				}
			}

			if (Tracks.TryGetValue("guitar", out var gtrArray)) {
				song.StemMaps[SongStem.Guitar] = new int[gtrArray.Length];
				for (int i = 0; i < gtrArray.Length; i++) {
					song.StemMaps[SongStem.Guitar][i] = gtrArray[i];
					mapped[gtrArray[i]] = true;
				}
			}

			if (Tracks.TryGetValue("vocals", out var voxArray)) {
				song.StemMaps[SongStem.Vocals] = new int[voxArray.Length];
				for (int i = 0; i < voxArray.Length; i++) {
					song.StemMaps[SongStem.Vocals][i] = voxArray[i];
					mapped[voxArray[i]] = true;
				}
			}

			if (Tracks.TryGetValue("keys", out var keysArray)) {
				song.StemMaps[SongStem.Keys] = new int[keysArray.Length];
				for (int i = 0; i < keysArray.Length; i++) {
					song.StemMaps[SongStem.Keys][i] = keysArray[i];
					mapped[keysArray[i]] = true;
				}
			}

			if (CrowdChannels != null) {
				song.StemMaps[SongStem.Crowd] = new int[CrowdChannels.Length];
				for (int i = 0; i < CrowdChannels.Length; i++) {
					song.StemMaps[SongStem.Crowd][i] = CrowdChannels[i];
					mapped[CrowdChannels[i]] = true;
				}
			}

			// every index in mapped that is still false, goes in the backing
			var fakeIndices = Enumerable.Range(0, mapped.Length).Where(i => !mapped[i]).ToList();
			song.StemMaps[SongStem.Song] = new int[fakeIndices.Count];
			for (int i = 0; i < fakeIndices.Count; i++) {
				song.StemMaps[SongStem.Song][i] = fakeIndices[i];
			}

			// END BASS Stem Mapping ------------------------------------------------------------------------

			// BEGIN BASS Matrix calculation ----------------------------------------------------------------

			song.MatrixRatios = new float[PanData.Length, 2];

			for(int i = 0; i < PanData.Length; i++){
				float theta = PanData[i] * ((float) Math.PI / 4);
				float ratioL = (float) (Math.Sqrt(2) / 2) * ((float) Math.Cos(theta) - (float) Math.Sin(theta));
				float ratioR = (float) (Math.Sqrt(2) / 2) * ((float) Math.Cos(theta) + (float) Math.Sin(theta));

				float volRatio = (float) Math.Pow(10, VolumeData[i] / 20);

				song.MatrixRatios[i, 0] = volRatio * ratioL;
				song.MatrixRatios[i, 1] = volRatio * ratioR;
			}

			// END BASS Matrix calculation ------------------------------------------------------------------

        }
    }
}