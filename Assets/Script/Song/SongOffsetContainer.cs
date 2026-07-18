using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using YARG.Core.Logging;
using YARG.Helpers;

namespace YARG.Song
{
    public static class SongOffsetContainer
    {
        private const string OFFSETS_FILENAME = "song_offsets.json";
        private static readonly string _offsetsPath = Path.Combine(PathHelper.PersistentDataPath, OFFSETS_FILENAME);
        private static readonly JsonSerializerSettings _offsetJsonSettings = new() { Formatting = Formatting.Indented };

        public static Dictionary<string, long> LoadOffsets()
        {
            if (!File.Exists(_offsetsPath))
            {
                return new Dictionary<string, long>();
            }
            try
            {
                return ParseOffsets(File.ReadAllText(_offsetsPath));
            }
            catch (Exception ex)
            {
                YargLogger.LogError(ex, "Failed to load song offsets");
                return new Dictionary<string, long>();
            }
        }

        public static long GetOffsetMilliseconds(string hashKey)
        {
            var offsets = LoadOffsets();
            offsets.TryGetValue(hashKey, out var offset);
            return offset;
        }

        private static Dictionary<string, long> ParseOffsets(string json)
        {
            var data = JsonConvert.DeserializeObject<Dictionary<string, long>>(json, _offsetJsonSettings);
            return data ?? new Dictionary<string, long>();
        }

        public static void SaveOffsets(Dictionary<string, long> offsets)
        {
            try
            {
                File.WriteAllText(_offsetsPath, SerializeOffsets(offsets));
            }
            catch (Exception ex)
            {
                ToastManager.ToastError(ex, "Failed to save song offsets");
            }
        }

        private static string SerializeOffsets(Dictionary<string, long> offsets)
        {
            return JsonConvert.SerializeObject(offsets, _offsetJsonSettings);
        }
    }
}
