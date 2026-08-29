using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using YARG.Core.Logging;
using YARG.Helpers;
using YARG.Menu.Persistent;

namespace YARG.Song
{
    public static class SongOffsetContainer
    {
        private const string OFFSETS_FILENAME = "song_offsets.json";
        private static readonly string _offsetsPath = Path.Combine(PathHelper.PersistentDataPath, OFFSETS_FILENAME);
        private static readonly JsonSerializerSettings _offsetJsonSettings = new() { Formatting = Formatting.Indented };

        // Fallback for recovering entries from a file that no longer parses as valid JSON
        // (e.g. corrupted by a crash mid-write, manual editing, etc). Matches the flat
        // "hashKey": offset shape the file is always written in.
        private static readonly Regex _entryPattern = new(
            @"""(?<key>(?:[^""\\]|\\.)*)""\s*:\s*(?<value>-?\d+)",
            RegexOptions.Compiled);

        public static Dictionary<string, long> LoadOffsets()
        {
            if (!File.Exists(_offsetsPath))
            {
                return new Dictionary<string, long>();
            }

            string text = File.ReadAllText(_offsetsPath);
            try
            {
                return ParseOffsets(text);
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, "Song offsets file is not valid JSON; recovering entries leniently");
                var recovered = RecoverOffsetsLeniently(text);
                YargLogger.LogFormatWarning("Recovered {0} song offset entries from a corrupted file", recovered.Count);
                return recovered;
            }
        }

        // Best-effort recovery of individual "key": value entries from a corrupted file, so a
        // failed parse doesn't discard offsets that are still intact elsewhere in the file.
        private static Dictionary<string, long> RecoverOffsetsLeniently(string text)
        {
            var result = new Dictionary<string, long>();
            foreach (Match match in _entryPattern.Matches(text))
            {
                if (long.TryParse(match.Groups["value"].Value, out var value))
                {
                    result[Regex.Unescape(match.Groups["key"].Value)] = value;
                }
            }
            return result;
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

        public static void SetOffsetMilliseconds(string hashKey, long offsetMilliseconds)
        {
            var offsets = LoadOffsets();

            if (offsetMilliseconds == 0)
            {
                offsets.Remove(hashKey);
            }
            else
            {
                offsets[hashKey] = offsetMilliseconds;
            }

            SaveOffsets(offsets);
        }

        public static void SaveOffsets(Dictionary<string, long> offsets)
        {
            try
            {
                File.WriteAllText(_offsetsPath, SerializeOffsets(offsets));
            }
            catch (Exception ex)
            {
                ToastManager.ToastError($"{ex} Failed to save song offsets");
            }
        }

        private static string SerializeOffsets(Dictionary<string, long> offsets)
        {
            return JsonConvert.SerializeObject(offsets, _offsetJsonSettings);
        }
    }
}
