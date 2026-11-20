using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using YARG.Core.Logging;
using YARG.Core.Song;
using YARG.Core.Utility;
using YARG.Helpers;

namespace YARG.Gameplay.Practice
{
    [Serializable]
    public class PracticeSessionData
    {
        public HashWrapper SongHash { get; set; }

        public int? StartSectionIndex { get; set; }
        public int? EndSectionIndex { get; set; }

        public double? LoopStartTime { get; set; }
        public double? LoopEndTime { get; set; }

        [JsonIgnore]
        public bool HasValidABLoop => LoopStartTime.HasValue && LoopEndTime.HasValue && LoopEndTime > LoopStartTime;

        [JsonIgnore]
        public bool HasValidSections => StartSectionIndex.HasValue && EndSectionIndex.HasValue;
    }

    public static class PracticeCacheManager
    {
        public static string PracticeDirectory { get; private set; }
        private static string _cachePath;

        private static readonly JsonSerializerSettings _jsonSettings = new()
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            Converters = new List<JsonConverter>
            {
                new JsonHashWrapperConverter()
            }
        };

        private static readonly Dictionary<HashWrapper, PracticeSessionData> _sessions = new();

        public static void Initialize()
        {
            PracticeDirectory = Path.Combine(PathHelper.PersistentDataPath, "practice");
            _cachePath = Path.Combine(PracticeDirectory, "practice_sessions.json");

            Directory.CreateDirectory(PracticeDirectory);
            _sessions.Clear();

            if (!File.Exists(_cachePath))
            {
                Save();
                return;
            }

            try
            {
                var text = File.ReadAllText(_cachePath);
                var entries = JsonConvert.DeserializeObject<List<PracticeSessionData>>(text, _jsonSettings);

                if (entries == null)
                {
                    return;
                }

                foreach (var entry in entries)
                {
                    if (entry != null && !entry.SongHash.Equals(default))
                    {
                        _sessions[entry.SongHash] = entry;
                    }
                }
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, "Failed to load practice sessions");
            }
        }

        public static void Save()
        {
            if (string.IsNullOrEmpty(_cachePath))
            {
                return;
            }

            try
            {
                var text = JsonConvert.SerializeObject(_sessions.Values, _jsonSettings);
                File.WriteAllText(_cachePath, text);
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, "Failed to save practice sessions");
            }
        }

        public static bool TryGetSession(HashWrapper songHash, out PracticeSessionData data)
        {
            return _sessions.TryGetValue(songHash, out data);
        }

        public static void SaveSections(HashWrapper songHash, int startIdx, int endIdx)
        {
            _sessions[songHash] = new PracticeSessionData
            {
                SongHash = songHash,
                StartSectionIndex = startIdx,
                EndSectionIndex = endIdx,
                LoopStartTime = null,
                LoopEndTime = null
            };

            Save();
        }

        public static void SaveABLoop(HashWrapper songHash, double startTime, double endTime)
        {
            if (!_sessions.TryGetValue(songHash, out var data))
            {
                data = new PracticeSessionData { SongHash = songHash };
                _sessions[songHash] = data;
            }

            data.LoopStartTime = startTime;
            data.LoopEndTime = endTime;

            Save();
        }

        public static void ClearABLoop(HashWrapper songHash)
        {
            if (_sessions.TryGetValue(songHash, out var data))
            {
                data.LoopStartTime = null;
                data.LoopEndTime = null;
                Save();
            }
        }

        public static int ReplaceUpdatedSongHashes(IReadOnlyDictionary<HashWrapper, HashWrapper> replacements)
        {
            if (replacements == null || replacements.Count == 0 || _sessions.Count == 0)
            {
                return 0;
            }

            int replaced = 0;
            var updatedEntries = new List<(HashWrapper OldHash, HashWrapper NewHash, PracticeSessionData Data)>();

            foreach (var (oldHash, data) in _sessions)
            {
                if (replacements.TryGetValue(oldHash, out var newHash))
                {
                    updatedEntries.Add((oldHash, newHash, data));
                }
            }

            foreach (var (oldHash, newHash, data) in updatedEntries)
            {
                _sessions.Remove(oldHash);
                data.SongHash = newHash;
                _sessions[newHash] = data;
                replaced++;
            }

            if (replaced > 0)
            {
                Save();
                YargLogger.LogInfo($"Replaced {replaced} updated song hashes in practice sessions");
            }

            return replaced;
        }
    }
}