using System;
using System.IO;
using YARG.Core.Extensions;

namespace YARG.Core.Song
{
    public struct RBMetadata
    {
        public static readonly RBMetadata Default = new()
        {
            SongID = string.Empty,
            DrumBank = string.Empty,
            VocalPercussionBank = string.Empty,
            VocalGender = true,
            Soloes = Array.Empty<string>(),
            VideoVenues = Array.Empty<string>(),
            RealGuitarTuning = Array.Empty<int>(),
            RealBassTuning = Array.Empty<int>(),
            Indices = RBAudio<int>.Empty,
            Panning = RBAudio<float>.Empty,
        };

        public string SongID;
        public uint AnimTempo;
        public string DrumBank;
        public string VocalPercussionBank;
        public uint VocalSongScrollSpeed;
        public bool VocalGender; //true for male, false for female
        //public bool HasAlbumArt;
        //public bool IsFake;
        public uint VocalTonicNote;
        public bool SongTonality; // 0 = major, 1 = minor
        public int TuningOffsetCents;
        public uint VenueVersion;

        public string[] Soloes;
        public string[] VideoVenues;

        public int[] RealGuitarTuning;
        public int[] RealBassTuning;

        public RBAudio<int> Indices;
        public RBAudio<float> Panning;

        public RBMetadata(UnmanagedMemoryStream stream)
        {
            AnimTempo = stream.Read<uint>(Endianness.Little);
            SongID = stream.ReadString();
            VocalPercussionBank = stream.ReadString();
            VocalSongScrollSpeed = stream.Read<uint>(Endianness.Little);
            VocalGender = stream.ReadBoolean();
            VocalTonicNote = stream.Read<uint>(Endianness.Little);
            SongTonality = stream.ReadBoolean();
            TuningOffsetCents = stream.Read<int>(Endianness.Little);
            VenueVersion = stream.Read<uint>(Endianness.Little);
            DrumBank = stream.ReadString();

            RealGuitarTuning = RBAudio<int>.ReadArray(stream);
            RealBassTuning = RBAudio<int>.ReadArray(stream);

            Indices = new RBAudio<int>(stream);
            Panning = new RBAudio<float>(stream);

            Soloes = ReadStringArray(stream);
            VideoVenues = ReadStringArray(stream);
        }

        public readonly void Serialize(MemoryStream stream)
        {
            stream.Write(AnimTempo, Endianness.Little);
            stream.Write(SongID);
            stream.Write(VocalPercussionBank);
            stream.Write(VocalSongScrollSpeed, Endianness.Little);
            stream.Write(VocalGender);
            stream.Write(VocalTonicNote, Endianness.Little);
            stream.Write(SongTonality);
            stream.Write(TuningOffsetCents, Endianness.Little);
            stream.Write(VenueVersion, Endianness.Little);
            stream.Write(DrumBank);

            RBAudio<int>.WriteArray(in RealGuitarTuning, stream);
            RBAudio<int>.WriteArray(in RealBassTuning, stream);

            Indices.Serialize(stream);
            Panning.Serialize(stream);

            stream.Write(Soloes.Length, Endianness.Little);
            for (int i = 0; i < Soloes.Length; ++i)
            {
                stream.Write(Soloes[i]);
            }

            stream.Write(VideoVenues.Length, Endianness.Little);
            for (int i = 0; i < VideoVenues.Length; ++i)
            {
                stream.Write(VideoVenues[i]);
            }
        }

        private static string[] ReadStringArray(UnmanagedMemoryStream stream)
        {
            int length = stream.Read<int>(Endianness.Little);
            if (length == 0)
            {
                return Array.Empty<string>();
            }

            var strings = new string[length];
            for (int i = 0; i < length; ++i)
            {
                strings[i] = stream.ReadString();
            }
            return strings;
        }
    }
}
