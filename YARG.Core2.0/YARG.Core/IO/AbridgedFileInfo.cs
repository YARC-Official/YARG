using System;
using System.IO;
using YARG.Core.Extensions;

namespace YARG.Core.IO
{
    /// <summary>
    /// A FileInfo structure that only contains the filename and time last added
    /// </summary>
    public readonly struct AbridgedFileInfo
    {
        public const FileAttributes RECALL_ON_DATA_ACCESS = (FileAttributes)0x00400000;

        /// <summary>
        /// The file path
        /// </summary>
        public readonly string FullName;

        /// <summary>
        /// The time the file was last written or created on OS - whichever came later
        /// </summary>
        public readonly DateTime LastUpdatedTime;

        public AbridgedFileInfo(string file, bool checkCreationTime = true)
            : this(new FileInfo(file), checkCreationTime) { }

        public AbridgedFileInfo(FileInfo info, bool checkCreationTime = true)
        {
            FullName = info.FullName;
            LastUpdatedTime = info.LastWriteTime;
            if (checkCreationTime && info.CreationTime > LastUpdatedTime)
            {
                LastUpdatedTime = info.CreationTime;
            }
        }

        /// <summary>
        /// Only used when validation of the underlying file is not required
        /// </summary>
        public AbridgedFileInfo(UnmanagedMemoryStream stream)
            : this(stream.ReadString(), stream) { }

        /// <summary>
        /// Only used when validation of the underlying file is not required
        /// </summary>
        public AbridgedFileInfo(string filename, UnmanagedMemoryStream stream)
        {
            FullName = filename;
            LastUpdatedTime = DateTime.FromBinary(stream.Read<long>(Endianness.Little));
        }

        public AbridgedFileInfo(string filename, in DateTime lastUpdatedTime)
        {
            FullName = filename;
            LastUpdatedTime = lastUpdatedTime;
        }

        public void Serialize(MemoryStream stream)
        {
            stream.Write(FullName);
            stream.Write(LastUpdatedTime.ToBinary(), Endianness.Little);
        }

        public bool Exists()
        {
            return File.Exists(FullName);
        }

        public bool IsStillValid(bool checkCreationTime = true)
        {
            var info = new FileInfo(FullName);
            if (!info.Exists)
            {
                return false;
            }

            var timeToCompare = info.LastWriteTime;
            if (checkCreationTime && info.CreationTime > timeToCompare)
            {
                timeToCompare = info.CreationTime;
            }
            return timeToCompare == LastUpdatedTime;
        }

        /// <summary>
        /// Used for cache validation
        /// </summary>
        public static AbridgedFileInfo? TryParseInfo(UnmanagedMemoryStream stream, bool checkCreationTime = true)
        {
            return TryParseInfo(stream.ReadString(), stream, checkCreationTime);
        }

        /// <summary>
        /// Used for cache validation
        /// </summary>
        public static AbridgedFileInfo? TryParseInfo(string file, UnmanagedMemoryStream stream, bool checkCreationTime = true)
        {
            var info = new FileInfo(file);
            if (!info.Exists)
            {
                stream.Position += sizeof(long);
                return null;
            }

            var abridged = new AbridgedFileInfo(info, checkCreationTime);
            if (abridged.LastUpdatedTime != DateTime.FromBinary(stream.Read<long>(Endianness.Little)))
            {
                return null;
            }
            return abridged;
        }
    }
}
