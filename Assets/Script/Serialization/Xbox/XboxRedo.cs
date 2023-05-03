using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using DtxCS.DataTypes;
using UnityEngine;
using XboxSTFS;
using YARG.Data;

namespace YARG.Serialization {
    public abstract class XboxSongAbs {
        public abstract byte[] GetMidiFile();
    }

    // TODO: fill this class out to be similar to XboxCONSong
    // and replace the existing XboxSong class with this one
    public class XboxRawSong : XboxSongAbs {

        public string shortname { get; private set; }
        public string rootPath { get; private set; }

        public XboxRawSong(string path, DataArray dta){
            rootPath = path;
        }
        public override byte[] GetMidiFile(){
            return File.ReadAllBytes($"{rootPath}/{shortname}/{shortname}.mid");
        }

    }

    public class XboxCONSong : XboxSongAbs {

        public string shortname { get; private set; }
        public string CONRootPath { get; private set; }

        private DataArray dta;

        private uint MidiSize;
        private uint[] MidiOffsets;

        private uint MoggSize;
        private uint[] MoggOffsets;
        
        private uint ImgSize;
        private uint[] ImgOffsets;

        XboxSongData songDta;
        XboxMoggData moggDta;
        XboxImage img;

        public XboxCONSong(string path, DataArray currentDTA, STFS theCON){
            // set CON file path, dta and song shortname
            CONRootPath = path;
            dta = currentDTA;
            shortname = dta.Name;

            // get file sizes and offsets in the CON's memory
            MidiSize = theCON.GetFileSize($"songs/{shortname}/{shortname}.mid");
            MidiOffsets = theCON.GetMemOffsets($"songs/{shortname}/{shortname}.mid");
            MoggSize = theCON.GetFileSize($"songs/{shortname}/{shortname}.mogg");
            MoggOffsets = theCON.GetMemOffsets($"songs/{shortname}/{shortname}.mogg");
            ImgSize = theCON.GetFileSize($"songs/{shortname}/gen/{shortname}_keep.png_xbox");
            ImgOffsets = theCON.GetMemOffsets($"songs/{shortname}/gen/{shortname}_keep.png_xbox");
        }

        public void ParseSong(){
            // first, parse songs.dta
			songDta = new XboxSongData();
			songDta.ParseFromDta(dta);

			// now, parse the mogg
			moggDta = new XboxMoggData(CONRootPath, MoggSize, MoggOffsets);
            moggDta.ParseMoggHeader();
			moggDta.ParseFromDta(dta.Array("song"));
			moggDta.CalculateMoggBassInfo();

            // finally, parse the image
            if(songDta.albumArt && ImgSize > 0 && ImgOffsets != null){
                img = new XboxImage(CONRootPath, ImgSize, ImgOffsets);
            }

        }

        public bool IsValidSong() {
			// Skip if the song doesn't have notes
            if(MidiSize == 0 && MidiOffsets == null) return false;
			// Skip if this is a "fake song" (tutorials, etc.)
			if (songDta.fake) return false;
			// Skip if the mogg is encrypted
			if (moggDta.Header != 0xA) return false;

			return true;
		}

        public override string ToString() {
			return string.Join(Environment.NewLine,
				$"XBOX CON SONG {shortname}",
				$"CON file: {CONRootPath}",
				"",
				songDta.ToString(),
				"",
				moggDta.ToString()
			);
		}

        public override byte[] GetMidiFile(){
            byte[] f = new byte[MidiSize];
            uint lastSize = MidiSize % 0x1000;

            Parallel.For(0, MidiOffsets.Length, i => {
                uint readLen = (i == MidiOffsets.Length - 1) ? lastSize : 0x1000;
                using var fs = new FileStream(CONRootPath, FileMode.Open, FileAccess.Read);
                using var br = new BinaryReader(fs, new ASCIIEncoding());
                fs.Seek(MidiOffsets[i], SeekOrigin.Begin);
                Array.Copy(br.ReadBytes((int)readLen), 0, f, i*0x1000, (int)readLen);
            });
            
            return f;
        }

        //TODO: convert each XboxCONSong to its own SongInfo for YARG to use in-game

    }
}