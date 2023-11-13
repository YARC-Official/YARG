using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using YARG.Core.Song;
using YARG.Data;
using YARG.Helpers;
using YARG.Song;

namespace YARG
{
    public static class ScoreManager
    {
        /// <value>
        /// The location of the score file.
        /// </value>
        public static string ScoreFile => Path.Combine(PathHelper.PersistentDataPath, "scores.json");

        private static Dictionary<HashWrapper, SongScore> scores = null;

        /// <summary>
        /// Should be called before you access any scores.
        /// </summary>
        public static async UniTask FetchScores()
        {
            if (scores != null)
            {
                return;
            }

            // Read from score file OR create new
            try
            {
                if (File.Exists(ScoreFile))
                {
                    string json = await File.ReadAllTextAsync(ScoreFile);
                    scores = await Task.Run(() => JsonConvert.DeserializeObject<Dictionary<HashWrapper, SongScore>>(json));
                }
                else
                {
                    scores = new();

                    // Create a dummy score file if one doesn't exist.
                    Directory.CreateDirectory(new FileInfo(ScoreFile).DirectoryName);
                    File.WriteAllText(ScoreFile.ToString(), "{}");
                }
            }
            catch (System.Exception e)
            {
                // If we fail to read the score file, so just create empty scores.
                scores = new();
                Debug.LogError("Failed to read scores file!");
                Debug.LogException(e);
            }
        }

        public static void PushScore(SongMetadata song, SongScore score)
        {
            if (!scores.TryGetValue(song.Hash, out var oldScore))
            {
                // If the score info doesn't exist, just add the new one.
                scores.Add(song.Hash, score);
            }
            else
            {
                // Otherwise, MERGE!
                oldScore.lastPlayed = score.lastPlayed;
                oldScore.timesPlayed += score.timesPlayed;

                // Create a highestScore dictionary for backwards compatibility (if null)
                oldScore.highestScore ??= new();

                // Merge high scores
                foreach (var kvp in score.highestPercent)
                {
                    // percent
                    if (oldScore.highestPercent.TryGetValue(kvp.Key, out var old))
                    {
                        if (old < kvp.Value)
                        {
                            oldScore.highestPercent[kvp.Key] = kvp.Value;
                        }
                    }
                    else
                    {
                        oldScore.highestPercent.Add(kvp.Key, kvp.Value);
                    }
                }

                foreach (var kvp in score.highestScore)
                {
                    // score
                    if (oldScore.highestScore.TryGetValue(kvp.Key, out var old))
                    {
                        if (old < kvp.Value)
                        {
                            oldScore.highestScore[kvp.Key] = kvp.Value;
                        }
                    }
                    else
                    {
                        oldScore.highestScore.Add(kvp.Key, kvp.Value);
                    }
                }
            }

            // Save ASAP!
            SaveScore();
        }

        public static SongScore GetScore(SongMetadata song)
        {
            if (scores.TryGetValue(song.Hash, out var o))
            {
                return o;
            }

            return null;
        }

        public static void SaveScore()
        {
            var scoreCopy = new Dictionary<HashWrapper, SongScore>(scores);

            // Prevent game lag by saving on another thread
            ThreadPool.QueueUserWorkItem(_ =>
            {
                string json = JsonConvert.SerializeObject(scores);
                File.WriteAllText(ScoreFile.ToString(), json);
            });
        }

        public static List<SongMetadata> SongsByPlayCount()
        {
            return scores
                .OrderByDescending(i => i.Value.lastPlayed)
                .Select(i =>
                {
                    if (GlobalVariables.Instance.SongContainer.SongsByHash.TryGetValue(i.Key, out var song))
                    {
                        return song[0];
                    }

                    return null;
                })
                .Where(i => i != null)
                .ToList();
        }

        /// <summary>
        /// Force reset scores. This makes the game re-scan if needed.
        /// </summary>
        public static void Reset()
        {
            scores = null;
        }
    }
}