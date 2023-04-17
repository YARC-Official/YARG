using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using DtxCS;
using DtxCS.DataTypes;
using UnityEngine;

// complete RB3 songs.dta attributes:
// shortname, name, artist, master, song_id,
// song (name, tracks (drum, bass, guitar, vocals, keys), vocal_parts, pans, vols, cores, drum_solo (seqs), drum_freestyle (seqs))
// bank (tambourine, cowbell, etc), drum_bank, band_fail_cue, anim_tempo, song_scroll_speed, preview, song_length
// rank (drum, guitar, bass, vocals, keys, real_keys, real_guitar, real_bass, band)
// solo (guitar drums bass keys vocal_percussion)
// format, version, game_origin, short_version, rating, genre, vocal_gender, year_released, year_recorded
// album_art, album_name, album_track_number, vocal_tonic_note, song_tonality,
// real_guitar_tuning, real_bass_tuning, tuning_offset_cents

// complete GH songs.dta attributes:
// shortname, name, artist, caption (performed_by means it's the original artist, if it's as made famous by, no caption)
// song (name, tracks (guitar bass), pans, vols, cores, midi_file)
// anim_tempo, preview
// quickplay (character_outfit, guitar, venue)

// complete GH DX songs.dta additions:
// artist (songalbum, author (as in chart author), songyear, songgenre, songorigin, 
//      songduration, songguitarrank, songbassrank, songrhythmrank, songdrumrank, songartist)

// common attributes:
// shortname, name, artist, master/caption,
// song (name, tracks, pans, vols, cores)
// anim_tempo, preview

namespace YARG.Serialization {
    public class XboxSongData {
        // all the possible metadata you could possibly want from a particular songs.dta
        private string shortname, name, artist, songPath;
        private string gameOrigin, genre, albumName, bank;
        private uint songId = 0, songLength = 0, songScrollSpeed = 2300;
        private short tuningOffsetCents = 0;
        private ushort? yearReleased, yearRecorded, vocalTonicNote;
        private bool master = false, albumArt, hasVocalTonicNote = false, hasSongTonality = false, songTonality = false, vocalGender; //vocalGender is true if male, false if female
        private byte rating = 4, albumTrackNumber, animTempo;
        private byte vocalParts = 1;
        private (uint start, uint end) preview;
        private float[] pans, vols;
        private short[] cores, realGuitarTuning, realBassTuning;
        private Dictionary<string, byte[]> tracks;
        private Dictionary<string, ushort> ranks;

        private static DataArray GetDataDict(DataArray dta, string key){ return dta.Array(key); }

        public XboxSongData ParseFromDataArray(DataArray dta){
            shortname = dta.Name;

            Parallel.For(1, dta.Count, i => {
                DataArray dtaArray = (DataArray)dta[i];
                switch(dtaArray[0].ToString()){
                    case "name": name = ((DataAtom)dtaArray[1]).Name; break;
                    case "artist": artist = ((DataAtom)dtaArray[1]).Name; break;
                    case "master":
                        if(dtaArray[1] is DataSymbol symMaster)
                            master = (symMaster.Name.ToUpper() == "TRUE");
                        else if(dtaArray[1] is DataAtom atmMaster)
                            master = (atmMaster.Int != 0);
                        break;
                    case "song_id": songId = (uint)((DataAtom)dtaArray[1]).Int; break;
                    case "song_length": songLength = (uint)((DataAtom)dtaArray[1]).Int; break;
                    case "song":
                        Debug.Log($"this is where the fun begins");
                        Parallel.For(1, dtaArray.Count, n => {
                            DataArray innerSongArray = (DataArray)dtaArray[n];
                            switch(innerSongArray[0].ToString()){
                                case "name":
                                    songPath = ((DataSymbol)innerSongArray[1]).Name;
                                    break;
                                case "pans":
                                    DataArray panArray = (DataArray)innerSongArray[1];
                                    pans = new float[panArray.Count];
                                    Parallel.For(0, panArray.Count, p => { pans[p] = ((DataAtom)panArray[p]).Float; });
                                    break;
                                case "vols":
                                    DataArray volArray = (DataArray)innerSongArray[1];
                                    vols = new float[volArray.Count];
                                    Parallel.For(0, volArray.Count, v => { vols[v] = ((DataAtom)volArray[v]).Float; });
                                    break;
                                case "cores":
                                    DataArray coreArray = (DataArray)innerSongArray[1];
                                    cores = new short[coreArray.Count];
                                    Parallel.For(0, coreArray.Count, c => { cores[c] = (short)((DataAtom)coreArray[c]).Int; });
                                    break;
                                case "vocal_parts":
                                    vocalParts = (byte)((DataAtom)innerSongArray[1]).Int;
                                    break;
                                // case "tracks": //TODO: implement
                                //     break;
                            }
                        });
                        break;
                    case "anim_tempo":
                        if(dtaArray[1] is DataSymbol symTempo)
                            switch(symTempo.Name){
                                case "kTempoSlow": animTempo = 16; break;
                                case "kTempoMedium": animTempo = 32; break;
                                case "kTempoFast": animTempo = 64; break;
                                default: animTempo = 0; break;
                            }
                        else if(dtaArray[1] is DataAtom atmTempo)
                            animTempo = (byte)((DataAtom)dtaArray[1]).Int;
                        break;
                    case "preview":
                        preview = ((uint)((DataAtom)dtaArray[1]).Int, (uint)((DataAtom)dtaArray[2]).Int);
                        break;
                    case "bank": bank = ((DataSymbol)dtaArray[1]).Name; break;
                    case "song_scroll_speed": songScrollSpeed = (uint)((DataAtom)dtaArray[1]).Int; break;
                    case "rank":
                        ranks = new Dictionary<string, ushort>();
                        Parallel.For(1, dtaArray.Count, j => {
                            if(dtaArray[j] is DataArray inner)
                                ranks.Add(((DataSymbol)inner[0]).Name, (ushort)((DataAtom)inner[1]).Int);
                        });
                        break;
                    case "game_origin": gameOrigin = ((DataSymbol)dtaArray[1]).Name; break;
                    case "genre": genre = ((DataSymbol)dtaArray[1]).Name; break;
                    case "rating": rating = (byte)((DataAtom)dtaArray[1]).Int; break;
                    case "vocal_gender": //true if male, false if female
                        vocalGender = (((DataSymbol)dtaArray[1]).Name == "male");
                        break;
                    case "album_art":
                        if(dtaArray[1] is DataSymbol symArt)
                            albumArt = (symArt.Name.ToUpper() == "TRUE");
                        else if(dtaArray[1] is DataAtom atmArt)
                            albumArt = (atmArt.Int != 0);
                        break;
                    case "album_name": albumName = ((DataAtom)dtaArray[1]).Name; break;
                    case "album_track_number": albumTrackNumber = (byte)((DataAtom)dtaArray[1]).Int; break;
                    case "year_released": yearReleased = (ushort)((DataAtom)dtaArray[1]).Int; break;
                    case "year_recorded": yearRecorded = (ushort)((DataAtom)dtaArray[1]).Int; break;
                    case "vocal_tonic_note":
                        hasVocalTonicNote = true;
                        vocalTonicNote = (ushort)((DataAtom)dtaArray[1]).Int;
                        break;
                    case "song_tonality": //0 = major, 1 = minor
                        hasSongTonality = true;
                        songTonality = ((((DataAtom)dtaArray[1]).Int) != 0);
                        break;
                    case "tuning_offset_cents": tuningOffsetCents = (short)((DataAtom)dtaArray[1]).Int; break;
                    case "real_guitar_tuning":
                        DataArray guitarTunes = (DataArray)dtaArray[1];
                        realGuitarTuning = new short[6];
                        Parallel.For(0, 6, g => { realGuitarTuning[g] = (short)((DataAtom)guitarTunes[g]).Int; });
                        break;
                    case "real_bass_tuning":
                        DataArray bassTunes = (DataArray)dtaArray[1];
                        realBassTuning = new short[4];
                        Parallel.For(0, 4, b => { realBassTuning[b] = (short)((DataAtom)bassTunes[b]).Int; });
                        break;
                    default:
                        if(dtaArray[1] is DataAtom dtaAtom)
                            Debug.Log($"ATOM: {dtaArray[0]} {dtaArray[1]}");
                        else if(dtaArray[1] is DataSymbol dtaSymbol)
                            Debug.Log($"SYMBOL: {dtaArray[0]}, {dtaArray[1]}");
                        else if(dtaArray[1] is DataArray innerArray)
                            Debug.Log($"ARRAY: {dtaArray[0]}, {dtaArray[1]}");
                        break;
                }
            });

            // get (song ) attributes (name, tracks, pans/vols/cores, etc)
            DataArray songArray = GetDataDict(dta, "song");

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
}
