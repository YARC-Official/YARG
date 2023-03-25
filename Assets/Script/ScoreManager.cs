using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;
using YARG.Data;
using YARG.Util;

namespace YARG {
	public static class ScoreManager {
		/// <value>
		/// The location of the local or remote score file (depending on whether we are connected to a server).
		/// </value>
		public static string ScoreFile => Path.Combine(SongLibrary.SongFolder, "yarg_score.json");

		private static Dictionary<string, SongScore> scores = null;

		/// <summary>
		/// Should be called before you access any scores.
		/// </summary>
		public static void FetchScores() {
			if (scores != null) {
				return;
			}

			// Read from score file OR create new
			if (File.Exists(ScoreFile)) {
				string json = File.ReadAllText(ScoreFile.ToString());
				scores = JsonConvert.DeserializeObject<Dictionary<string, SongScore>>(json);
			} else {
				scores = new();

				// Create a dummy score file if one doesn't exist.
				Directory.CreateDirectory(new FileInfo(ScoreFile).DirectoryName);
				File.WriteAllText(ScoreFile.ToString(), "{}");
			}
		}

		public static void PushScore(SongInfo song, SongScore score) {
			string path = song.folder.ToString();
			if (GameManager.client != null) {
				path = song.realFolderRemote.ToString();
			}
			path = path.ToUpperInvariant();

			if (!scores.TryGetValue(path, out var oldScore)) {
				// If the score info doesn't exist, just add the new one.
				scores.Add(path, score);
			} else {
				// Otherwise, MERGE!
				oldScore.lastPlayed = score.lastPlayed;
				oldScore.timesPlayed += score.timesPlayed;

				// Merge high scores
				foreach (var kvp in score.highestPercent) {
					if (oldScore.highestPercent.TryGetValue(kvp.Key, out var old)) {
						if (old < kvp.Value) {
							oldScore.highestPercent[kvp.Key] = kvp.Value;
						}
					} else {
						oldScore.highestPercent.Add(kvp.Key, kvp.Value);
					}
				}
			}

			// Save ASAP!
			SaveScore();
		}

		public static SongScore GetScore(SongInfo song) {
			string path = song.folder.ToString();
			if (song.realFolderRemote != null && GameManager.client != null) {
				path = song.realFolderRemote.ToString();
			}
			path = path.ToUpperInvariant();

			if (scores.TryGetValue(path, out var o)) {
				return o;
			}

			return null;
		}

		public static void SaveScore() {
			var scoreCopy = new Dictionary<string, SongScore>(scores);

			// Prevent game lag by saving on another thread
			ThreadPool.QueueUserWorkItem(_ => {
				string json = JsonConvert.SerializeObject(scores, Formatting.Indented);
				File.WriteAllText(ScoreFile.ToString(), json);

				// If remote, write scores on server
				if (GameManager.client != null) {
					GameManager.client.WriteScores();
				}
			});
		}

		public static List<SongInfo> SongsByPlayCount() {
			return scores
				.OrderByDescending(i => i.Value.lastPlayed)
				.Select(i => SongLibrary.Songs.Find(j => Utils.ArePathsEqual(j.folder.FullName, i.Key)))
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