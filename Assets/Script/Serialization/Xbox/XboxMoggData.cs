using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DtxCS;
using DtxCS.DataTypes;
using UnityEngine;
using YARG;

namespace YARG.Serialization {
    public class XboxMoggData {
        private string moggPath;
        private byte channelCount;
        private int header, startMoggAddress;
        private long usableMoggLength;
        private float[] pans, vols;
        private Dictionary<string, byte[]> tracks;
        private byte[] crowdChannels;

        private Dictionary<SongStem,byte[]> stemMaps;
        private float[,] matrixRatios;

        public XboxMoggData(string str){ moggPath = str; }

        public void ParseMoggHeader(){
            byte[] buffer = new byte[4];
            using(FileStream fs = new FileStream(moggPath, FileMode.Open, FileAccess.Read)){
                using(BinaryReader br = new BinaryReader(fs, new ASCIIEncoding())){
                    buffer = br.ReadBytes(4);
                    header = BitConverter.ToInt32(buffer,0);
                    buffer = br.ReadBytes(4);
                    startMoggAddress = BitConverter.ToInt32(buffer,0);
                    usableMoggLength = fs.Length - startMoggAddress;
                }
            }
        }

        public int GetHeaderVersion() { return header; }

        public void ParseFromDta(DataArray dta){
            for(int i = 1; i < dta.Count; i++){
                DataArray dtaArray = (DataArray)dta[i];
                switch(dtaArray[0].ToString()){
                    case "tracks":
                        DataArray trackArray = (DataArray)dtaArray[1];
                        tracks = new Dictionary<string, byte[]>();
                        for(int x = 0; x < trackArray.Count; x++){
                            string key = "";
                            byte[] val = null;
                            if(trackArray[x] is DataArray instrArray){
                                key = ((DataSymbol)instrArray[0]).Name;
                                if(instrArray[1] is DataArray trackNums){
                                    if(trackNums.Count > 0){
                                        val = new byte[trackNums.Count];
                                        for(int y = 0; y < trackNums.Count; y++)
                                            val[y] = (byte)((DataAtom)trackNums[y]).Int;
                                        tracks.Add(key, val);
                                    }
                                }
                                else if(instrArray[1] is DataAtom trackNum){
                                    val = new byte[1];
                                    val[0] = (byte)trackNum.Int;
                                    tracks.Add(key, val);
                                }
                            }
                        }
                        break;
                    case "pans":
                        DataArray panArray = (DataArray)dtaArray[1];
                        pans = new float[panArray.Count];
                        for(int p = 0; p < panArray.Count; p++) pans[p] = ((DataAtom)panArray[p]).Float;
                        channelCount = (byte)panArray.Count;
                        break;
                    case "vols":
                        DataArray volArray = (DataArray)dtaArray[1];
                        vols = new float[volArray.Count];
                        for(int v = 0; v < volArray.Count; v++) vols[v] = ((DataAtom)volArray[v]).Float;
                        break;
                    case "crowd_channels":
                        crowdChannels = new byte[dtaArray.Count - 1];
                        for(int cc = 1; cc < dtaArray.Count; cc++)
                            crowdChannels[cc - 1] = (byte)((DataAtom)dtaArray[cc]).Int;
                        break;
                }
            }
        }

        public override string ToString(){
            string debugTrackStr = "";
            foreach(var kvp in tracks) debugTrackStr += $"{kvp.Key}, ({string.Join(", ", kvp.Value)}) ";

            return string.Join(Environment.NewLine,
                $"Mogg metadata:",
                $"channel count: {channelCount}",
                $"tracks={string.Join(", ", debugTrackStr)}",
                $"pans=({string.Join(", ", pans)})",
                $"vols=({string.Join(", ", vols)})"
            );
        }

        public void CalculateMoggBASSInfo(){
            stemMaps = new Dictionary<SongStem, byte[]>();
            bool[] mapped = new bool[channelCount];

            // BEGIN BASS Stem Mapping ----------------------------------------------------------------------

            if(tracks.TryGetValue("drum", out var drumArray)){
                switch(drumArray.Length){
                    //drum (0 1): stereo kit --> (0 1)
                    case 2:
                        stemMaps[SongStem.Drums] = new byte[] {drumArray[0], drumArray[1]};
                        break;
                    //drum (0 1 2): mono kick, stereo snare/kit --> (0) (1 2)
                    case 3:
                        stemMaps[SongStem.Drums] = new byte[] {drumArray[0]};
                        stemMaps[SongStem.Drums1] = new byte[] {drumArray[1], drumArray[2]};
                        break;
                    //drum (0 1 2 3): mono kick, mono snare, stereo kit --> (0) (1) (2 3)
                    case 4:
                        stemMaps[SongStem.Drums] = new byte[] {drumArray[0]};
                        stemMaps[SongStem.Drums1] = new byte[] {drumArray[1]};
                        stemMaps[SongStem.Drums2] = new byte[] {drumArray[2], drumArray[3]};
                        break;
                    //drum (0 1 2 3 4): mono kick, stereo snare, stereo kit --> (0) (1 2) (3 4)
                    case 5:
                        stemMaps[SongStem.Drums] = new byte[] {drumArray[0]};
                        stemMaps[SongStem.Drums1] = new byte[] {drumArray[1], drumArray[2]};
                        stemMaps[SongStem.Drums2] = new byte[] {drumArray[3], drumArray[4]};
                        break;
                    //drum (0 1 2 3 4 5): stereo kick, stereo snare, stereo kit --> (0 1) (2 3) (4 5)
                    case 6:
                        stemMaps[SongStem.Drums] = new byte[] {drumArray[0], drumArray[1]};
                        stemMaps[SongStem.Drums1] = new byte[] {drumArray[2], drumArray[3]};
                        stemMaps[SongStem.Drums2] = new byte[] {drumArray[4], drumArray[5]};
                        break;
                    default:
                        break;
                }
                for(int i = 0; i < drumArray.Length; i++) mapped[drumArray[i]] = true;
            }

            if(tracks.TryGetValue("bass", out var bassArray)){
                stemMaps[SongStem.Bass] = new byte[bassArray.Length];
                for(int i = 0; i < bassArray.Length; i++){
                    stemMaps[SongStem.Bass][i] = bassArray[i];
                    mapped[bassArray[i]] = true;
                }
            }

            if(tracks.TryGetValue("guitar", out var gtrArray)){
                stemMaps[SongStem.Guitar] = new byte[gtrArray.Length];
                for(int i = 0; i < gtrArray.Length; i++){
                    stemMaps[SongStem.Guitar][i] = gtrArray[i];
                    mapped[gtrArray[i]] = true;
                }
            }

            if(tracks.TryGetValue("vocals", out var voxArray)){
                stemMaps[SongStem.Vocals] = new byte[voxArray.Length];
                for(int i = 0; i < voxArray.Length; i++){
                    stemMaps[SongStem.Vocals][i] = voxArray[i];
                    mapped[voxArray[i]] = true;
                }
            }

            if(tracks.TryGetValue("keys", out var keysArray)){
                stemMaps[SongStem.Keys] = new byte[keysArray.Length];
                for(int i = 0; i < keysArray.Length; i++){
                    stemMaps[SongStem.Keys][i] = keysArray[i];
                    mapped[keysArray[i]] = true;
                }
            }

            if(crowdChannels != null){
                stemMaps[SongStem.Crowd] = new byte[crowdChannels.Length];
                for(int i = 0; i < crowdChannels.Length; i++){
                    stemMaps[SongStem.Crowd][i] = crowdChannels[i];
                    mapped[crowdChannels[i]] = true;
                }
            }

            // every index in mapped that is still false, goes in the backing
            List<int> fakeIndices = Enumerable.Range(0, mapped.Length).Where(i => !mapped[i]).ToList();
            stemMaps[SongStem.Song] = new byte[fakeIndices.Count];
            for(int i = 0; i < fakeIndices.Count; i++){
                stemMaps[SongStem.Song][i] = (byte)fakeIndices[i];
            }

            // END BASS Stem Mapping ------------------------------------------------------------------------

            // BEGIN BASS Matrix calculation ----------------------------------------------------------------

            float[,] matrixRatios = new float[pans.Length, 2];

            Parallel.For(0, pans.Length, i => {
                float theta = pans[i] * ((float)Math.PI / 4);
                float ratioL = (float)(Math.Sqrt(2) / 2) * ((float)Math.Cos(theta) - (float)Math.Sin(theta));
                float ratioR = (float)(Math.Sqrt(2) / 2) * ((float)Math.Cos(theta) + (float)Math.Sin(theta));

                float volRatio = (float)Math.Pow(10, vols[i] / 20);

                matrixRatios[i, 0] = volRatio * ratioL;
                matrixRatios[i, 1] = volRatio * ratioR;
            });

            // END BASS Matrix calculation ------------------------------------------------------------------

        }

        public Dictionary<SongStem,byte[]> GetSongStemMapping(){ return stemMaps; }
        public float[,] GetMoggMatrix(){ return matrixRatios; }
        
    }
}