using System;
using System.Collections.Generic;
using System.IO;
using YARG.Core.Extensions;
using YARG.Core.IO;

namespace YARG.Core.Song.Cache
{
    public sealed class UpdateGroup : IModificationGroup
    {
        public readonly string Directory;
        public readonly DateTime DTALastWrite;
        public readonly Dictionary<string, SongUpdate> Updates = new();
        public readonly FixedArray<byte> DTAData;

        public UpdateGroup(string directory, DateTime lastWrite, FixedArray<byte> data, Dictionary<string, SongUpdate> updates)
        {
            DTAData = data;
            Directory = directory;
            DTALastWrite = lastWrite;
            Updates = updates;
        }

        public void SerializeModifications(MemoryStream stream)
        {
            stream.Write(Directory);
            stream.Write(DTALastWrite.ToBinary(), Endianness.Little);
            stream.Write(Updates.Count, Endianness.Little);
            foreach (var (name, update) in Updates)
            {
                stream.Write(name);
                update.Serialize(stream);
            }
        }
    }

    public class SongUpdate
    {
        public readonly List<YARGTextContainer<byte>> Containers;
        public readonly AbridgedFileInfo? Midi;
        public readonly AbridgedFileInfo? Mogg;
        public readonly AbridgedFileInfo? Milo;
        public readonly AbridgedFileInfo? Image;

        internal SongUpdate(in AbridgedFileInfo? midi, in AbridgedFileInfo? mogg, in AbridgedFileInfo? milo, in AbridgedFileInfo? image)
        {
            Containers = new();
            Midi = midi;
            Mogg = mogg;
            Milo = milo;
            Image = image;
        }

        public void Serialize(MemoryStream stream)
        {
            WriteInfo(Midi, stream);
            WriteInfo(Mogg, stream);
            WriteInfo(Milo, stream);
            WriteInfo(Image, stream);

            static void WriteInfo(in AbridgedFileInfo? info, MemoryStream stream)
            {
                if (info != null)
                {
                    stream.Write(true);
                    stream.Write(info.Value.LastUpdatedTime.ToBinary(), Endianness.Little);
                }
                else
                {
                    stream.Write(false);
                }
            }
        }

        public bool Validate(UnmanagedMemoryStream stream)
        {
            if (!CheckInfo(in Midi, stream))
            {
                SkipInfo(stream);
                SkipInfo(stream);
                SkipInfo(stream);
                return false;
            }

            if (!CheckInfo(in Mogg, stream))
            {
                SkipInfo(stream);
                SkipInfo(stream);
                return false;
            }

            if (!CheckInfo(in Milo, stream))
            {
                SkipInfo(stream);
                return false;
            }
            return CheckInfo(in Image, stream);

            static bool CheckInfo(in AbridgedFileInfo? info, UnmanagedMemoryStream stream)
            {
                if (!stream.ReadBoolean())
                {
                    return info == null;
                }

                var lastWrite = DateTime.FromBinary(stream.Read<long>(Endianness.Little));
                return info != null && info.Value.LastUpdatedTime == lastWrite;
            }
        }

        public static void SkipRead(UnmanagedMemoryStream stream)
        {
            SkipInfo(stream);
            SkipInfo(stream);
            SkipInfo(stream);
            SkipInfo(stream);
        }

        private static void SkipInfo(UnmanagedMemoryStream stream)
        {
            if (stream.ReadBoolean())
            {
                stream.Position += CacheHandler.SIZEOF_DATETIME;
            }
        }
    }
}
