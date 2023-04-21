using System.Collections;
using System.Collections.Generic;
using DtxCS;
using DtxCS.DataTypes;
using UnityEngine;

namespace YARG.Serialization {
    public class XboxSong {
        private string shortname;
        private string songFolderPath;
        private XboxSongData songDta;
        private XboxMoggData moggDta;
        private XboxImage img;

        public XboxSong(string pathName, DataArray dta){
            songDta = new XboxSongData(dta); // get song metadata from songs.dta
            shortname = songDta.GetShortName();
            songFolderPath = pathName + "/" + shortname; // get song folder path for mid, mogg, png_xbox

            Debug.Log($"song folder path: {songFolderPath}");
            Debug.Log(songDta.ToString());

            // parse the mogg
            moggDta = new XboxMoggData($"{songFolderPath}/{shortname}.mogg");
            moggDta.ParseMoggHeader();
            moggDta.ParseFromDta(dta.Array("song")); // get mogg metadata from songs.dta
            moggDta.CalculateMoggBASSInfo();
            Debug.Log(moggDta.ToString());

            // parse the image
            if(songDta.AlbumArtRequired()){
                img = new XboxImage($"{songFolderPath}/gen/{shortname}_keep.png_xbox");
                // do some preliminary parsing here in the header to get DXT format, width and height, etc
                img.ParseImageHeader();
            }
        }

        // true if this song is good to go and can be shown in-game, false if not
        public bool ValidateSong(){ return (!songDta.IsFake() && (moggDta.GetHeaderVersion() == 0xA)); }

        // TODO: implement this fxn
        // public SongInfo ConvertToSongInfo()
    }
}