using System;
using System.IO;
using System.Runtime.InteropServices.ComTypes;
using System.Text.RegularExpressions;
using YARG.Core.Extensions;
using YARG.Core.Game;
using YARG.Core.IO;
using YARG.Core.Logging;
using YARG.Core.Song;
using YARG.Core.Utility;

namespace YARG.Core.Replays
{
    public enum ReplayReadResult
    {
        Valid,
        MetadataOnly,
        InvalidVersion,
        DataMismatch,
        NotAReplay,
        Corrupted,
        FileNotFound,
    }

    public static class ReplayIO
    {
        private static readonly EightCC REPLAY_MAGIC_HEADER_OLD = new('Y', 'A', 'R', 'G', 'P', 'L', 'A', 'Y');
        private static readonly EightCC REPLAY_MAGIC_HEADER = new('Y', 'A', 'R', 'E', 'P', 'L', 'A', 'Y');

        private static readonly (int OLD_MIN, int METADATA_MIN, int DATA_MIN, int CURRENT) REPLAY_VERSIONS = (4, 6, 7, 7);
        private const int ENGINE_VERSION = 2;

        public static (ReplayReadResult Result, ReplayInfo Info, ReplayData Data) TryDeserialize(string path)
        {
            try
            {
                using var fstream = File.OpenRead(path);
                if (!REPLAY_MAGIC_HEADER.Matches(fstream))
                {
                    fstream.Position = 0;
                    // Old replays don't have their actual data deserialized
                    if (REPLAY_MAGIC_HEADER_OLD.Matches(fstream))
                    {
                        var (result, info_old) = ReadInfo_Old(path, fstream);
                        return (result, info_old, null!);
                    }
                    else
                    {
                        return (ReplayReadResult.NotAReplay, null!, null!);
                    }
                }

                var headerHash = HashWrapper.Deserialize(fstream);
                int headerLength = fstream.Read<int>(Endianness.Little);
                using var headerArray = FixedArray<byte>.Read(fstream, headerLength);
                if (!headerHash.Equals(HashWrapper.Hash(headerArray.ReadOnlySpan)))
                {
                    return (ReplayReadResult.Corrupted, null!, null!);
                }

                using var headerStream = headerArray.ToStream();
                var info = new ReplayInfo(path, headerStream);
                if (info.ReplayVersion < REPLAY_VERSIONS.METADATA_MIN || info.ReplayVersion > REPLAY_VERSIONS.CURRENT)
                {
                    return (ReplayReadResult.InvalidVersion, null!, null!);
                }

                // Ensures a cutoff for only legible data
                if (info.ReplayVersion < REPLAY_VERSIONS.DATA_MIN)
                {
                    return (ReplayReadResult.MetadataOnly, info, null!);
                }

                using var data = FixedArray<byte>.ReadRemainder(fstream);
                if (!info.ReplayChecksum.Equals(HashWrapper.Hash(data.ReadOnlySpan)))
                {
                    return (ReplayReadResult.Corrupted, null!, null!);
                }

                using var dataStream = data.ToStream();
                var replayData = new ReplayData(dataStream, info.ReplayVersion);
                return (ReplayReadResult.Valid, info, replayData);
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, "Failed to read replay file");
                return (ReplayReadResult.Corrupted, null!, null!);
            }
        }

        public static (ReplayReadResult Result, ReplayInfo Info) TryReadMetadata(string path)
        {
            try
            {
                using var fstream = File.OpenRead(path);
                if (!REPLAY_MAGIC_HEADER.Matches(fstream))
                {
                    fstream.Position = 0;
                    // Old replays use a different read pattern with fewer checks
                    if (REPLAY_MAGIC_HEADER_OLD.Matches(fstream))
                    {
                        return ReadInfo_Old(path, fstream);
                    }
                    else
                    {
                        return (ReplayReadResult.NotAReplay, null!);
                    }
                }

                var headerHash = HashWrapper.Deserialize(fstream);
                int headerLength = fstream.Read<int>(Endianness.Little);
                using var headerArray = FixedArray<byte>.Read(fstream, headerLength);
                if (!headerHash.Equals(HashWrapper.Hash(headerArray.ReadOnlySpan)))
                {
                    return (ReplayReadResult.Corrupted, null!);
                }

                using var headerStream = headerArray.ToStream();
                var info = new ReplayInfo(path, headerStream);
                if (info.ReplayVersion < REPLAY_VERSIONS.METADATA_MIN || info.ReplayVersion > REPLAY_VERSIONS.CURRENT)
                {
                    return (ReplayReadResult.InvalidVersion, null!);
                }

                if (info.ReplayVersion < REPLAY_VERSIONS.DATA_MIN)
                {
                    return (ReplayReadResult.MetadataOnly, info);
                }

                // The stream-based hashing function provides better efficiency in this instance
                // as we don't read the entire file.
                //
                // We still want to check the integrity here so that it doesn't error out when attempting to load
                // the file in its entirety later.
                using var algo = HashWrapper.Algorithm;
                var hash = algo.ComputeHash(fstream);
                if (!info.ReplayChecksum.Equals(HashWrapper.Create(hash)))
                {
                    return (ReplayReadResult.Corrupted, null!);
                }
                return (ReplayReadResult.Valid, info);
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, "Failed to read replay file");
                return (ReplayReadResult.Corrupted, null!);
            }
        }

        public static (ReplayReadResult Result, ReplayData Data) TryLoadData(ReplayInfo info)
        {
            try
            {
                using var fstream = File.OpenRead(info.FilePath);
                if (!REPLAY_MAGIC_HEADER.Matches(fstream))
                {
                    fstream.Position = 0;
                    // Old replays don't have their actual data deserialized
                    if (REPLAY_MAGIC_HEADER_OLD.Matches(fstream))
                    {
                        // If true, someone did a no-no and swapped the file
                        if (info.ReplayVersion >= REPLAY_VERSIONS.METADATA_MIN)
                        {
                            return (ReplayReadResult.DataMismatch, null!);
                        }

                        int replayVersion_old = fstream.Read<int>(Endianness.Little);
                        int engineVersion_old = fstream.Read<int>(Endianness.Little);
                        var replayChecksum_old = HashWrapper.Deserialize(fstream);
                        // If true, someone did a no-no and swapped the file
                        if (replayVersion_old != info.ReplayVersion || info.EngineVersion != engineVersion_old || !info.ReplayChecksum.Equals(replayChecksum_old))
                        {
                            return (ReplayReadResult.DataMismatch, null!);
                        }
                    }
                    return (ReplayReadResult.InvalidVersion, null!);
                }

                // If true, someone did a no-no and swapped the file
                if (info.ReplayVersion < REPLAY_VERSIONS.METADATA_MIN)
                {
                    return (ReplayReadResult.DataMismatch, null!);
                }

                var headerHash = HashWrapper.Deserialize(fstream);
                int headerLength = fstream.Read<int>(Endianness.Little);
                using var headerArray = FixedArray<byte>.Read(fstream, headerLength);
                if (!headerHash.Equals(HashWrapper.Hash(headerArray.ReadOnlySpan)))
                {
                    return (ReplayReadResult.Corrupted, null!);
                }

                using var headerStream = headerArray.ToStream();
                int replayVersion = headerStream.Read<int>(Endianness.Little);
                int engineVersion = headerStream.Read<int>(Endianness.Little);
                var replayChecksum = HashWrapper.Deserialize(headerStream);
                // If true, someone did a no-no and swapped the file
                if (replayVersion != info.ReplayVersion || info.EngineVersion != engineVersion || !info.ReplayChecksum.Equals(replayChecksum))
                {
                    return (ReplayReadResult.DataMismatch, null!);
                }

                using var data = FixedArray<byte>.ReadRemainder(fstream);
                if (!info.ReplayChecksum.Equals(HashWrapper.Hash(data.ReadOnlySpan)))
                {
                    return (ReplayReadResult.Corrupted, null!);
                }

                using var dataStream = data.ToStream();
                var replayData = new ReplayData(dataStream, info.ReplayVersion);
                return (ReplayReadResult.Valid, replayData);
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, "Failed to read replay file");
                return (ReplayReadResult.Corrupted, null!);
            }
        }

        public static (bool Success, ReplayInfo Info) TrySerialize(string directory, SongEntry song, float speed, double length, int score, StarAmount stars, ReplayStats[] stats, ReplayData data)
        {
            try
            {
                // Write all the data for the replay hash
                var replayData = data.Serialize();
                var replayChecksum = HashWrapper.Hash(replayData);

                var date = DateTime.Now;
                var replayName = ReplayInfo.ConstructReplayName(song.Name, song.Artist, song.Charter, in date);

                var path = Path.Combine(directory, replayName + ".replay");
                var info = new ReplayInfo(path, replayName, REPLAY_VERSIONS.CURRENT, ENGINE_VERSION, in replayChecksum, song.Name, song.Artist, song.Charter, song.Hash, in date, speed, length, score, stars, stats);

                // Write all the data for the header hash
                using var headerStream = new MemoryStream();
                using var headerWriter = new BinaryWriter(headerStream);
                info.Serialize(headerWriter);

                var headerData = new ReadOnlySpan<byte>(headerStream.GetBuffer(), 0, (int) headerStream.Length);
                var headerChecksum = HashWrapper.Hash(headerData);

                // Write all processed data to the file
                using var fstream = File.OpenWrite(path);
                using var fileWriter = new BinaryWriter(fstream);
                REPLAY_MAGIC_HEADER.Serialize(fileWriter);
                headerChecksum.Serialize(fileWriter);
                fileWriter.Write(headerData.Length);
                fileWriter.Write(headerData);
                fileWriter.Write(replayData);
                return (true, info);
            }
            catch (Exception e)
            {
                YargLogger.LogException(e, "Failed to save replay to file");
                return (false, null!);
            }
        }

        private static (ReplayReadResult, ReplayInfo) ReadInfo_Old(string path, FileStream fstream)
        {
            // This value can't be correctly parsed from old data due to version inconsistencies
            // + It originally only came from EngineParameters
            const float DEFAULT_SPEED = 1.0f;
            int replayVersion = fstream.Read<int>(Endianness.Little);
            if (replayVersion < REPLAY_VERSIONS.OLD_MIN || replayVersion >= REPLAY_VERSIONS.METADATA_MIN)
            {
                return (ReplayReadResult.InvalidVersion, null!);
            }

            int engineVersion = fstream.Read<int>(Endianness.Little);
            var replayChecksum = HashWrapper.Deserialize(fstream);

            using var data = FixedArray<byte>.ReadRemainder(fstream);
            if (!replayChecksum.Equals(HashWrapper.Hash(data.ReadOnlySpan)))
            {
                return (ReplayReadResult.Corrupted, null!);
            }

            using var memStream = data.ToStream();
            var song = memStream.ReadString();
            var artist = memStream.ReadString();
            var charter = memStream.ReadString();
            var score = memStream.Read<int>(Endianness.Little);
            var stars = (StarAmount) memStream.ReadByte();
            var length = memStream.Read<double>(Endianness.Little);
            var date = DateTime.FromBinary(memStream.Read<long>(Endianness.Little));
            var songChecksum = HashWrapper.Deserialize(memStream);

            var replayName = ReplayInfo.ConstructReplayName(song, artist, charter, in date);
            var info = new ReplayInfo(path, replayName, replayVersion, engineVersion, in replayChecksum, song, artist, charter, in songChecksum, in date, DEFAULT_SPEED, length, score, stars, Array.Empty<ReplayStats>());
            return (ReplayReadResult.MetadataOnly, info);
        }
    }
}
