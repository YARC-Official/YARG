using System.Collections.Generic;
using System;
using System.IO;
using YARG.Core.Extensions;
using YARG.Core.Game;
using Newtonsoft.Json;
using YARG.Core.Utility;

namespace YARG.Core.Replays
{
    public class ReplayData
    {
        private const int PRESETS_VERSION = 0;
        private readonly Dictionary<Guid, ColorProfile> _colorProfiles;
        private readonly Dictionary<Guid, CameraPreset> _cameraPresets;
        public readonly ReplayFrame[] Frames;

        public int PlayerCount => Frames.Length;

        public ReplayData(Dictionary<Guid, ColorProfile> colors, Dictionary<Guid, CameraPreset> cameras, ReplayFrame[] frames)
        {
            _colorProfiles = colors;
            _cameraPresets = cameras;
            Frames = frames;
        }

        public ReplayData(UnmanagedMemoryStream stream, int version)
        {
            int _ = stream.Read<int>(Endianness.Little);
            _colorProfiles = DeserializeDict<ColorProfile>(stream);
            _cameraPresets = DeserializeDict<CameraPreset>(stream);

            int count = stream.Read<int>(Endianness.Little);
            Frames = new ReplayFrame[count];
            for (int i = 0; i != count; i++)
            {
                Frames[i] = new ReplayFrame(stream, version);
            }
        }

        public ReadOnlySpan<byte> Serialize()
        {
            // Write all the data for the replay hash
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);

            writer.Write(PRESETS_VERSION);
            SerializeDict(writer, _colorProfiles);
            SerializeDict(writer, _cameraPresets);

            writer.Write(Frames.Length);
            foreach (var frame in Frames)
            {
                frame.Serialize(writer);
            }
            return new ReadOnlySpan<byte>(stream.GetBuffer(), 0, (int) stream.Length);
        }

        /// <returns>
        /// The color profile if it's in this container, otherwise, <c>null</c>.
        /// </returns>
        public ColorProfile? GetColorProfile(Guid guid)
        {
            _colorProfiles.TryGetValue(guid, out var color);
            return color;
        }

        /// <returns>
        /// The camera preset if it's in this container, otherwise, <c>null</c>.
        /// </returns>
        public CameraPreset? GetCameraPreset(Guid guid)
        {
            _cameraPresets.TryGetValue(guid, out var preset);
            return preset;
        }

        private static readonly JsonSerializerSettings _jsonSettings = new()
        {
            Converters =
            {
                new JsonColorConverter()
            }
        };

        private static void SerializeDict<T>(BinaryWriter writer, Dictionary<Guid, T> dict)
        {
            writer.Write(dict.Count);
            foreach (var (key, value) in dict)
            {
                // Write key
                writer.Write(key);

                // Write preset
                var json = JsonConvert.SerializeObject(value, _jsonSettings);
                writer.Write(json);
            }
        }

        private static Dictionary<Guid, T> DeserializeDict<T>(Stream stream)
        {
            var dict = new Dictionary<Guid, T>();
            int len = stream.Read<int>(Endianness.Little);
            for (int i = 0; i < len; i++)
            {
                // Read key
                var guid = stream.ReadGuid();

                // Read preset
                var json = stream.ReadString();
                var preset = JsonConvert.DeserializeObject<T>(json, _jsonSettings)!;

                dict.Add(guid, preset);
            }
            return dict;
        }
    }
}
