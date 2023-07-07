using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DtxCS;
using DtxCS.DataTypes;
using UnityEngine;
using YARG.Audio;
using YARG.Data;
using YARG.Song;

namespace YARG.Serialization
{
    public static class MoggBASSInfoGenerator
    {
        public static void Generate(ExtractedConSongEntry song, DataArray dta, List<DataArray> dta_update_roots = null)
        {
            float[] pan = null, volume = null;
            DataArray dta_update;
            List<DataArray> dtas_to_parse = new()
            {
                dta
            };
            
            // determine whether or not we even NEED to parse the update dta for mogg information
            if (dta_update_roots != null)
            {
                foreach (var dta_update_root in dta_update_roots)
                {
                    dta_update = dta_update_root.Array("song");
                    if (dta_update != null)
                    {
                        if (dta_update.Array("tracks") != null || dta_update.Array("pans") != null ||
                            dta_update.Array("vols") != null || dta_update.Array("crowd_channels") != null)
                            dtas_to_parse.Add(dta_update);
                    }
                }
            }
            
            foreach (var dta_to_parse in dtas_to_parse)
            {
                for (int i = 1; i < dta_to_parse.Count; i++)
                {
                    var dtaArray = (DataArray) dta_to_parse[i];
                    switch (dtaArray[0].ToString())
                    {
                        case "tracks":
                            var trackArray = (DataArray) dtaArray[1];
                            for (int x = 0; x < trackArray.Count; x++)
                            {
                                if (trackArray[x] is not DataArray instrArray) continue;
                                
                                string key = ((DataSymbol) instrArray[0]).Name;
                                int[] val;
                                if (instrArray[1] is DataArray trackNums)
                                {
                                    if (trackNums.Count <= 0)
                                        continue;
                                    val = new int[trackNums.Count];
                                    for (int y = 0; y < trackNums.Count; y++)
                                        val[y] = ((DataAtom) trackNums[y]).Int;
                                }
                                else if (instrArray[1] is DataAtom trackNum)
                                    val = new[] { trackNum.Int };
                                else
                                    continue;
                                
                                switch (key)
                                {
                                    case "drum":
                                        song.DrumIndices = val;
                                        break;
                                    case "bass":
                                        song.BassIndices = val;
                                        break;
                                    case "guitar":
                                        song.GuitarIndices = val;
                                        break;
                                    case "keys":
                                        song.KeysIndices = val;
                                        break;
                                    case "vocals":
                                        song.VocalsIndices = val;
                                        break;
                                }
                            }
                            break;
                        case "pans":
                            var panArray = dtaArray[1] as DataArray;
                            pan = new float[panArray.Count];
                            for (int p = 0; p < panArray.Count; p++) pan[p] = ((DataAtom) panArray[p]).Float;
                            break;
                        case "vols":
                            var volArray = dtaArray[1] as DataArray;
                            volume = new float[volArray.Count];
                            for (int v = 0; v < volArray.Count; v++)
                            {
                                var volAtom = (DataAtom) volArray[v];
                                if (volAtom.Type == DataType.FLOAT)
                                    volume[v] = ((DataAtom) volArray[v]).Float;
                                else
                                    volume[v] = ((DataAtom) volArray[v]).Int;
                            }
                            break;
                        case "crowd_channels":
                            {
                                int[] val = new int[dtaArray.Count - 1];
                                for (int cc = 1; cc < dtaArray.Count; cc++)
                                    val[cc - 1] = ((DataAtom) dtaArray[cc]).Int;
                                song.CrowdIndices = val;
                            }
                            break;
                    }
                }
            }
            
            if (pan != null && volume != null)
            {
                HashSet<int> pending = new();
                for (int i = 0; i < pan.Length; i++)
                    pending.Add(i);
                
                if (song.DrumIndices != Array.Empty<int>())
                    song.DrumStemValues = CalculateStemValues(song.DrumIndices, pan, volume, pending);
                
                if (song.BassIndices != Array.Empty<int>())
                    song.BassStemValues = CalculateStemValues(song.BassIndices, pan, volume, pending);
                
                if (song.GuitarIndices != Array.Empty<int>())
                    song.GuitarStemValues = CalculateStemValues(song.GuitarIndices, pan, volume, pending);
                
                if (song.KeysIndices != Array.Empty<int>())
                    song.KeysStemValues = CalculateStemValues(song.KeysIndices, pan, volume, pending);
                
                if (song.VocalsIndices != Array.Empty<int>())
                    song.VocalsStemValues = CalculateStemValues(song.VocalsIndices, pan, volume, pending);
                
                if (song.CrowdIndices != Array.Empty<int>())
                    song.CrowdStemValues = CalculateStemValues(song.CrowdIndices, pan, volume, pending);
                
                song.TrackIndices = pending.ToArray();
                song.TrackStemValues = CalculateStemValues(song.TrackIndices, pan, volume, pending);
            }
        }
        
        private static float[] CalculateStemValues(int[] indices, float[] pan, float[] volume, HashSet<int> pending)
        {
            float[] values = new float[2 * indices.Length];
            for (int i = 0; i < indices.Length; i++)
            {
                int index = indices[i];
                float theta = (pan[index] + 1) * ((float) Math.PI / 4);
                float volRatio = (float) Math.Pow(10, volume[index] / 20);
                values[2 * i] = volRatio * (float) Math.Cos(theta);
                values[2 * i + 1] = volRatio * (float) Math.Sin(theta);
                pending.Remove(index);
            }
            return values;
        }
    }
}