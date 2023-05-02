using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DtxCS.DataTypes;
using Newtonsoft.Json;

namespace YARG.Serialization {
	/*
	
	TODO: Generalize for all .mogg's
	
	*/

	[JsonObject(MemberSerialization.OptOut)]
	public class XboxMoggData {
		public string MoggPath { get; set; }
		public int ChannelCount { get; set; }
		public int Header { get; set; }

		private uint MoggSize = 0;
		private uint[] MoggOffsets = null;
		private bool isFromCON = false;

		public int MoggAddressAudioOffset { get; set; }
		public long MoggAudioLength { get; set; }

		public float[] PanData { get; set; }
		public float[] VolumeData { get; set; }

		public Dictionary<string, int[]> tracks;
		public int[] crowdChannels;

		public Dictionary<SongStem, int[]> stemMaps;
		public float[,] matrixRatios;

		public XboxMoggData(string str) {
			MoggPath = str;
		}

		public XboxMoggData(string str, uint size, uint[] offsets) {
			MoggPath = str;
			MoggSize = size;
			MoggOffsets = offsets;
			isFromCON = true;
		}

		public void ParseMoggHeader() {
			using var fs = new FileStream(MoggPath, FileMode.Open, FileAccess.Read);
			using var br = new BinaryReader(fs);
			if (isFromCON) fs.Seek(MoggOffsets[0], SeekOrigin.Begin);

			Header = br.ReadInt32();
			MoggAddressAudioOffset = br.ReadInt32();

			if (isFromCON) MoggAudioLength = MoggSize - MoggAddressAudioOffset;
			else MoggAudioLength = fs.Length - MoggAddressAudioOffset;
		}

		public byte[] GetOggDataFromMogg() {
			if (!isFromCON) //Raw
				return File.ReadAllBytes(MoggPath)[MoggAddressAudioOffset..];
			else { //CON
				byte[] f = new byte[MoggSize];
				uint lastSize = MoggSize % 0x1000;

				Parallel.For(0, MoggOffsets.Length, i => {
					uint readLen = (i == MoggOffsets.Length - 1) ? lastSize : 0x1000;
					using var fs = new FileStream(MoggPath, FileMode.Open, FileAccess.Read);
					using var br = new BinaryReader(fs, new ASCIIEncoding());
					fs.Seek(MoggOffsets[i], SeekOrigin.Begin);
					Array.Copy(br.ReadBytes((int) readLen), 0, f, i * 0x1000, (int) readLen);
				});

				return f[MoggAddressAudioOffset..];
			}
		}

		public void ParseFromDta(DataArray dta) {
			for (int i = 1; i < dta.Count; i++) {
				var dtaArray = (DataArray) dta[i];

				switch (dtaArray[0].ToString()) {
					case "tracks":
						var trackArray = (DataArray) dtaArray[1];
						tracks = new Dictionary<string, int[]>();

						for (int x = 0; x < trackArray.Count; x++) {
							if (trackArray[x] is not DataArray instrArray) continue;

							string key = ((DataSymbol) instrArray[0]).Name;
							int[] val;
							if (instrArray[1] is DataArray trackNums) {
								if (trackNums.Count <= 0) continue;

								val = new int[trackNums.Count];
								for (int y = 0; y < trackNums.Count; y++)
									val[y] = ((DataAtom) trackNums[y]).Int;
								tracks.Add(key, val);
							} else if (instrArray[1] is DataAtom trackNum) {
								val = new int[1];
								val[0] = trackNum.Int;
								tracks.Add(key, val);
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
						for (int v = 0; v < volArray.Count; v++) VolumeData[v] = ((DataAtom) volArray[v]).Float;
						break;
					case "crowd_channels":
						crowdChannels = new int[dtaArray.Count - 1];
						for (int cc = 1; cc < dtaArray.Count; cc++)
							crowdChannels[cc - 1] = ((DataAtom) dtaArray[cc]).Int;
						break;
				}
			}
		}

		public override string ToString() {
			string debugTrackStr = "";
			foreach (var kvp in tracks) {
				debugTrackStr += $"{kvp.Key}, ({string.Join(", ", kvp.Value)}) ";
			}

			return string.Join(Environment.NewLine,
				$"Mogg metadata:",
				$"channel count: {ChannelCount}",
				$"tracks={string.Join(", ", debugTrackStr)}",
				$"pans=({string.Join(", ", PanData)})",
				$"vols=({string.Join(", ", VolumeData)})"
			);
		}

		public void CalculateMoggBassInfo() {
			stemMaps = new Dictionary<SongStem, int[]>();
			var mapped = new bool[ChannelCount];

			// BEGIN BASS Stem Mapping ----------------------------------------------------------------------

			if (tracks.TryGetValue("drum", out var drumArray)) {
				switch (drumArray.Length) {
					//drum (0 1): stereo kit --> (0 1)
					case 2:
						stemMaps[SongStem.Drums] = new[] { drumArray[0], drumArray[1] };
						break;
					//drum (0 1 2): mono kick, stereo snare/kit --> (0) (1 2)
					case 3:
						stemMaps[SongStem.Drums1] = new[] { drumArray[0] };
						stemMaps[SongStem.Drums2] = new[] { drumArray[1], drumArray[2] };
						break;
					//drum (0 1 2 3): mono kick, mono snare, stereo kit --> (0) (1) (2 3)
					case 4:
						stemMaps[SongStem.Drums1] = new[] { drumArray[0] };
						stemMaps[SongStem.Drums2] = new[] { drumArray[1] };
						stemMaps[SongStem.Drums3] = new[] { drumArray[2], drumArray[3] };
						break;
					//drum (0 1 2 3 4): mono kick, stereo snare, stereo kit --> (0) (1 2) (3 4)
					case 5:
						stemMaps[SongStem.Drums1] = new[] { drumArray[0] };
						stemMaps[SongStem.Drums2] = new[] { drumArray[1], drumArray[2] };
						stemMaps[SongStem.Drums3] = new[] { drumArray[3], drumArray[4] };
						break;
					//drum (0 1 2 3 4 5): stereo kick, stereo snare, stereo kit --> (0 1) (2 3) (4 5)
					case 6:
						stemMaps[SongStem.Drums1] = new[] { drumArray[0], drumArray[1] };
						stemMaps[SongStem.Drums2] = new[] { drumArray[2], drumArray[3] };
						stemMaps[SongStem.Drums3] = new[] { drumArray[4], drumArray[5] };
						break;
				}

				foreach (int arr in drumArray) {
					mapped[arr] = true;
				}
			}

			if (tracks.TryGetValue("bass", out var bassArray)) {
				stemMaps[SongStem.Bass] = new int[bassArray.Length];
				for (int i = 0; i < bassArray.Length; i++) {
					stemMaps[SongStem.Bass][i] = bassArray[i];
					mapped[bassArray[i]] = true;
				}
			}

			if (tracks.TryGetValue("guitar", out var gtrArray)) {
				stemMaps[SongStem.Guitar] = new int[gtrArray.Length];
				for (int i = 0; i < gtrArray.Length; i++) {
					stemMaps[SongStem.Guitar][i] = gtrArray[i];
					mapped[gtrArray[i]] = true;
				}
			}

			if (tracks.TryGetValue("vocals", out var voxArray)) {
				stemMaps[SongStem.Vocals] = new int[voxArray.Length];
				for (int i = 0; i < voxArray.Length; i++) {
					stemMaps[SongStem.Vocals][i] = voxArray[i];
					mapped[voxArray[i]] = true;
				}
			}

			if (tracks.TryGetValue("keys", out var keysArray)) {
				stemMaps[SongStem.Keys] = new int[keysArray.Length];
				for (int i = 0; i < keysArray.Length; i++) {
					stemMaps[SongStem.Keys][i] = keysArray[i];
					mapped[keysArray[i]] = true;
				}
			}

			if (crowdChannels != null) {
				stemMaps[SongStem.Crowd] = new int[crowdChannels.Length];
				for (int i = 0; i < crowdChannels.Length; i++) {
					stemMaps[SongStem.Crowd][i] = crowdChannels[i];
					mapped[crowdChannels[i]] = true;
				}
			}

			// every index in mapped that is still false, goes in the backing
			var fakeIndices = Enumerable.Range(0, mapped.Length).Where(i => !mapped[i]).ToList();
			stemMaps[SongStem.Song] = new int[fakeIndices.Count];
			for (int i = 0; i < fakeIndices.Count; i++) {
				stemMaps[SongStem.Song][i] = fakeIndices[i];
			}

			// END BASS Stem Mapping ------------------------------------------------------------------------

			// BEGIN BASS Matrix calculation ----------------------------------------------------------------

			matrixRatios = new float[PanData.Length, 2];

			Parallel.For(0, PanData.Length, i => {
				float theta = PanData[i] * ((float) Math.PI / 4);
				float ratioL = (float) (Math.Sqrt(2) / 2) * ((float) Math.Cos(theta) - (float) Math.Sin(theta));
				float ratioR = (float) (Math.Sqrt(2) / 2) * ((float) Math.Cos(theta) + (float) Math.Sin(theta));

				float volRatio = (float) Math.Pow(10, VolumeData[i] / 20);

				matrixRatios[i, 0] = volRatio * ratioL;
				matrixRatios[i, 1] = volRatio * ratioR;
			});

			// END BASS Matrix calculation ------------------------------------------------------------------
		}
	}
}