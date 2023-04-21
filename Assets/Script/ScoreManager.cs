using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;
using UnityEngine;
using YARG.Data;

namespace YARG {
	public static class ScoreManager {
		/// <value>
		/// The location of the score file.
		/// </value>
		public static string ScoreFile => Path.Combine(GameManager.PersistentDataPath, "scores.json");

		private static Dictionary<string, SongScore> scores = null;

		/// <summary>
		/// Should be called before you access any scores.
		/// </summary>
		public static void FetchScores() {
			if (scores != null) {
				return;
			}

			// Read from score file OR create new
			try {
				if (File.Exists(ScoreFile)) {
					string json = File.ReadAllText(ScoreFile.ToString());
					scores = JsonConvert.DeserializeObject<Dictionary<string, SongScore>>(json);
				} else {
					scores = new();

					// Create a dummy score file if one doesn't exist.
					Directory.CreateDirectory(new FileInfo(ScoreFile).DirectoryName);
					File.WriteAllText(ScoreFile.ToString(), "{}");
				}
			} catch (System.Exception e) {
				// If we fail to read the score file, so just create empty scores.
				scores = new();
				Debug.LogException(e);
			}
		}

		public static void PushScore(SongInfo song, SongScore score) {
			if (!scores.TryGetValue(song.hash, out var oldScore)) {
				// If the score info doesn't exist, just add the new one.
				scores.Add(song.hash, score);
			} else {
				// Otherwise, MERGE!
				oldScore.lastPlayed = score.lastPlayed;
				oldScore.timesPlayed += score.timesPlayed;

				// Merge high scores
				foreach (var kvp in score.highestPercent) { // percent
					if (oldScore.highestPercent.TryGetValue(kvp.Key, out var old)) {
						if (old < kvp.Value) {
							oldScore.highestPercent[kvp.Key] = kvp.Value;
						}
					} else {
						oldScore.highestPercent.Add(kvp.Key, kvp.Value);
					}
				}
				foreach (var kvp in score.highestScore) { // score
					if (oldScore.highestScore.TryGetValue(kvp.Key, out var old)) {
						if (old < kvp.Value) {
							oldScore.highestScore[kvp.Key] = kvp.Value;
						}
					} else {
						oldScore.highestScore.Add(kvp.Key, kvp.Value);
					}
				}
			}

			// Save ASAP!
			SaveScore();
		}

		public static SongScore GetScore(SongInfo song) {
			if (scores.TryGetValue(song.hash, out var o)) {
				return o;
			}

			return null;
		}

		public static void SaveScore() {
			var scoreCopy = new Dictionary<string, SongScore>(scores);

			// Prevent game lag by saving on another thread
			ThreadPool.QueueUserWorkItem(_ => {
				string json = JsonConvert.SerializeObject(scores);
				File.WriteAllText(ScoreFile.ToString(), json);
			});
		}

		public static List<SongInfo> SongsByPlayCount() {
			return scores
				.OrderByDescending(i => i.Value.lastPlayed)
				.Select(i => {
					if (SongLibrary.SongsByHash.TryGetValue(i.Key, out var song)) {
						return song;
					}

					return null;
				})
				.Where(i => i != null)
				.ToList();
		}

		/// <summary>
		/// Force reset scores. This makes the game re-scan if needed.
		/// </summary>
		public static void Reset() {
			scores = null;
		}
	}
}