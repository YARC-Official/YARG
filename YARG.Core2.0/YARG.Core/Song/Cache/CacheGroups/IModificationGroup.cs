using System.Collections.Generic;
using System.IO;
using YARG.Core.Extensions;

namespace YARG.Core.Song.Cache
{
    public interface IModificationGroup
    {
        public void SerializeModifications(MemoryStream stream);

        public static void SerializeGroups<TGroup>(List<TGroup> groups, FileStream fileStream)
            where TGroup : IModificationGroup
        {
            var streams = new MemoryStream[groups.Count];
            int length = sizeof(int);
            for (int i = 0; i < groups.Count; i++)
            {
                var stream = streams[i] = new MemoryStream();
                groups[i].SerializeModifications(stream);
                length += sizeof(int) + (int) stream.Length;
            }

            fileStream.Write(length, Endianness.Little);
            fileStream.Write(streams.Length, Endianness.Little);
            for (int i = 0; i < groups.Count; i++)
            {
                using var stream = streams[i];
                fileStream.Write((int) stream.Length, Endianness.Little);
                fileStream.Write(stream.GetBuffer(), 0, (int) stream.Length);
            }
        }
    }
}
