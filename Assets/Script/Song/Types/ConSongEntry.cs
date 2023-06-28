using System.IO;
using DtxCS.DataTypes;
using XboxSTFS;
using System.Collections.Generic;
using System;

namespace YARG.Song
{
    public class ConSongEntry : ExtractedConSongEntry
    {
        // Location: the path to the CON file

        private XboxSTFSFile conFile;
        public int MidiIndex { get; private set; } = -1;
        public int MoggIndex { get; private set; } = -1;
        public int MiloIndex { get; private set; } = -1;
        public int ImgIndex { get; private set; } = -1;

        public ConSongEntry(BinaryReader reader, List<XboxSTFSFile> conFiles, string folder) : base(reader, folder)
        {
            ContinueCacheRead(reader, conFiles);

            string filename = reader.ReadString();
            foreach (var con in conFiles)
            {
                if (con.Filename == filename)
                {
                    conFile = con;
                    break;
                }
            }

            if (conFile == null) throw new ConMissingException();

            MidiIndex = reader.ReadInt32();
            MoggIndex = reader.ReadInt32();
            if (MoggIndex == -1) MoggPath = reader.ReadString();

            MiloIndex = reader.ReadInt32();
            if (MiloIndex == -1) MiloPath = reader.ReadString();

            ImgIndex = reader.ReadInt32();
            if (ImgIndex == -1)
            {
                ImagePath = reader.ReadString();
                HasAlbumArt = AlternatePath = ImagePath.Length > 0;
            }
        }

        public override void WriteMetadataToCache(BinaryWriter writer)
        {
            WriteMetadataToCache(writer, SongType.RbCon);
            writer.Write(conFile.Filename);
            writer.Write(MidiIndex);
            writer.Write(MoggIndex);
            if (MoggIndex == -1) writer.Write(MoggPath);

            writer.Write(MiloIndex);
            if (MiloIndex == -1) writer.Write(MiloPath);

            writer.Write(ImgIndex);
            if (ImgIndex == -1)
            {
                if (!AlternatePath)
                    writer.Write(string.Empty);
                else
                    writer.Write(ImagePath);
            }
        }

        public ConSongEntry(XboxSTFSFile conFile, DataArray dta) : base(dta)
        {
            this.conFile = conFile;

            string dir = Path.Combine("songs", Location);

            NotesFile = Path.Combine(dir, $"{Location}.mid");
            MidiIndex = conFile.GetFileIndex(NotesFile);

            string moggPath = Path.Combine(dir, $"{Location}.mogg");
            MoggIndex = conFile.GetFileIndex(moggPath);

            string miloPath = Path.Combine(dir, "gen", $"{Location}.milo_xbox");
            int miloVal = conFile.GetFileIndex(miloPath);
            if (miloVal != -1) MiloIndex = miloVal;

            string imgPath = Path.Combine(dir, "gen", $"{Location}_keep.png_xbox");
            int imgVal = conFile.GetFileIndex(imgPath);
            if (imgVal != -1) ImgIndex = imgVal;

            Location = conFile.Filename;
        }

        public override bool ValidateMidiFile()
        {
            return MidiIndex != -1;
        }

        public override void Update(string folder)
        {
            base.Update(folder);
            if (UsingUpdateMogg) MoggIndex = -1;

            if (UsingUpdateMilo) MiloIndex = -1;

            if (!HasAlbumArt || AlternatePath) ImgIndex = -1;
        }

        public override byte[] LoadMidiFile()
        {
            return conFile.LoadSubFile(MidiIndex);
        }

        public override byte[] LoadMoggFile()
        {
            // For some reason `UsingUpdateMogg` is false here.
            // TODO: Redo CON scanning, because it's quite odd
            // if (UsingUpdateMogg) {
            if (MoggIndex == -1)
            {
                return base.LoadMoggFile();
            }

            return conFile.LoadSubFile(MoggIndex);
        }

        public override byte[] LoadMiloFile()
        {
            if (UsingUpdateMilo) return base.LoadMiloFile();
            if (MiloIndex != -1) return conFile.LoadSubFile(MiloIndex);
            return Array.Empty<byte>();
        }

        public override byte[] LoadImgFile()
        {
            if (AlternatePath) return base.LoadImgFile();
            if (ImgIndex != -1) return conFile.LoadSubFile(ImgIndex);
            return Array.Empty<byte>();
        }

        public override bool IsMoggUnencrypted()
        {
            if (UsingUpdateMogg)
            {
                return base.IsMoggUnencrypted();
            }

            return conFile.IsMoggUnencrypted(MoggIndex);
        }
    }
}