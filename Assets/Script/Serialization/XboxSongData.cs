using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using DtxCS;
using DtxCS.DataTypes;
using UnityEngine;

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
        private string ghOutfit, ghGuitar, ghVenue;
        private uint songLength = 0, songScrollSpeed = 2300;
        private short tuningOffsetCents = 0;
        private uint? songId;
        private ushort? yearReleased, yearRecorded, vocalTonicNote; //TODO: implement other nullable/optional variables with default values
        private bool? songTonality;
        private bool master = false, albumArt = false, vocalGender; //vocalGender is true if male, false if female
        private byte rating = 4, animTempo;
        private byte? albumTrackNumber;
        private byte vocalParts = 1;
        private bool fake = false;
        private (uint start, uint end) preview;
        private byte[] crowdChannels;
        private float[] pans, vols;
        private short[] cores, realGuitarTuning, realBassTuning;
        private string[] solos;
        private Dictionary<string, byte[]> tracks;
        private Dictionary<string, ushort> ranks;

        private static DataArray GetDataDict(DataArray dta, string key){ return dta.Array(key); }

        //TODO: implement macro support, such as #ifndef kControllerRealGuitar, or #ifdef YARG
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
                    case "caption": master = true; break; //used in GH
                    case "song_id": songId = (uint)((DataAtom)dtaArray[1]).Int; break;
                    case "song_length": songLength = (uint)((DataAtom)dtaArray[1]).Int; break;
                    case "song":
                        Parallel.For(1, dtaArray.Count, n => {
                            DataArray innerSongArray = (DataArray)dtaArray[n];
                            switch(innerSongArray[0].ToString()){
                                case "name": 
                                    if(innerSongArray[1] is DataSymbol symPath)
                                        songPath = symPath.Name;
                                    else if(innerSongArray[1] is DataAtom atmPath)
                                        songPath = atmPath.Name;
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
                                case "vocal_parts": vocalParts = (byte)((DataAtom)innerSongArray[1]).Int; break;
                                case "crowd_channels":
                                    crowdChannels = new byte[innerSongArray.Count - 1];
                                    for(int cc = 1; cc < innerSongArray.Count; cc++)
                                        crowdChannels[cc - 1] = (byte)((DataAtom)innerSongArray[cc]).Int;
                                    break;
                                case "tracks":
                                    DataArray trackArray = (DataArray)innerSongArray[1];
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
                    case "solo":
                        DataArray soloInstruments = (DataArray)dtaArray[1];
                        solos = new string[soloInstruments.Count];
                        for(int t = 0; t < soloInstruments.Count; t++)
                            if(soloInstruments[t] is DataSymbol symSolo)
                                solos[t] = symSolo.Name;
                        break;
                    case "rank":
                        ranks = new Dictionary<string, ushort>();
                        for(int j = 1; j < dtaArray.Count; j++)
                            if(dtaArray[j] is DataArray inner)
                                ranks.Add(((DataSymbol)inner[0]).Name, (ushort)((DataAtom)inner[1]).Int);
                        break;
                    case "game_origin": gameOrigin = ((DataSymbol)dtaArray[1]).Name; break;
                    case "genre": genre = ((DataSymbol)dtaArray[1]).Name; break;
                    case "rating": rating = (byte)((DataAtom)dtaArray[1]).Int; break;
                    case "vocal_gender": vocalGender = (((DataSymbol)dtaArray[1]).Name == "male"); break;
                    case "fake": fake = (dtaArray[1].ToString().ToUpper() == "TRUE"); break;
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
                    case "vocal_tonic_note": vocalTonicNote = (ushort)((DataAtom)dtaArray[1]).Int; break;
                    case "song_tonality": songTonality = ((((DataAtom)dtaArray[1]).Int) != 0); break; //0 = major, 1 = minor
                    case "tuning_offset_cents": tuningOffsetCents = (short)((DataAtom)dtaArray[1]).Int; break;
                    case "real_guitar_tuning":
                        DataArray guitarTunes = (DataArray)dtaArray[1];
                        realGuitarTuning = new short[6];
                        for(int g = 0; g < 6; g++) realGuitarTuning[g] = (short)((DataAtom)guitarTunes[g]).Int;
                        break;
                    case "real_bass_tuning":
                        DataArray bassTunes = (DataArray)dtaArray[1];
                        realBassTuning = new short[4];
                        for(int b = 0; b < 4; b++) realBassTuning[b] = (short)((DataAtom)bassTunes[b]).Int;
                        break;
                    case "quickplay": //used in GH
                        for(int q = 1; q < dtaArray.Count; q++){
                            DataArray innerQPArray = (DataArray)dtaArray[q];
                            switch(innerQPArray[0].ToString()){
                                case "character_outfit": ghOutfit = ((DataSymbol)innerQPArray[1]).Name; break;
                                case "guitar": ghGuitar = ((DataSymbol)innerQPArray[1]).Name; break;
                                case "venue": ghVenue = ((DataSymbol)innerQPArray[1]).Name; break;
                            }
                        }
                        break;
                    default:
                        break;
                }
            });

            // must be done after the above parallel loop due to race issues with ranks and vocalParts
            if(!ranks.ContainsKey("vocals") || ranks["vocals"] == 0) vocalParts = 0;

            return this;
        }

        public string GetShortName(){ return shortname; }

        public override string ToString(){
            string debugTrackStr = "";
            foreach(var kvp in tracks) debugTrackStr += $"{kvp.Key}, ({string.Join(", ", kvp.Value)}) ";

            return string.Join(Environment.NewLine,
                $"song id={songId}; shortname={shortname}: name={name}; artist={((!master) ? "as made famous by " : "")}{artist}",
                $"song path={songPath}; vocal parts={vocalParts}; vocal gender={((vocalGender) ? "male" : "female")}",
                $"pans=({string.Join(", ", pans)})",
                $"vols=({string.Join(", ", vols)})",
                $"cores=({string.Join(", ", cores)})",
                // $"crowd channels=({string.Join(", ", crowdChannels)})",
                $"tracks={string.Join(", ", debugTrackStr)}",
                $"ranks={string.Join(", ", ranks)}",
                $"album art={albumArt}; album name={albumName}; album track number={albumTrackNumber}",
                $"year released={yearReleased}; year recorded={yearRecorded}",
                $"song length={songLength}; preview={preview}; game origin={gameOrigin}; genre={genre}; rating={rating}",
                $"vocal tonic note={vocalTonicNote}",
                $"song tonality={songTonality}",
                $"tuning offset cents={tuningOffsetCents}",
                $"real guitar tuning=({((realGuitarTuning != null) ? string.Join(", ", realGuitarTuning) : "")})",
                $"real bass tuning=({((realBassTuning != null) ? string.Join(", ", realBassTuning) : "")})"
            );
        }
    }
}
