using System.Collections.Generic;
using System.Linq;

using UnityEngine;

using YARG.Data;
using YARG.Util;

namespace YARG.PlayMode {
	/// <summary>
	/// Star-score tracking.
	/// </summary>
	public class StarScoreKeeper {
		/// <summary>
		/// Minimum avg. multipliers to get 1, 2, 3, 4, 5, and gold stars respectively.
		/// </summary>
		public static readonly Dictionary<string, float[]> instrumentThreshold = new() {
			{ "guitar", new float[] { .21f, .46f, .77f, 1.85f, 3.08f, 4.52f } },
			{ "bass", new float[] { .21f, .5f, .9f, 2.77f, 4.62f, 6.78f } },
			{ "keys", new float[] { .21f, .46f, .77f, 1.85f, 3.08f, 4.52f } },
			{ "realGuitar", new float[] { .21f, .46f, .77f, 1.85f, 3.08f, 4.52f } },
			{ "realBass", new float[] { .21f, .46f, .77f, 1.85f, 3.08f, 4.52f } },
			{ "drums", new float[] { .21f, .46f, .77f, 1.85f, 3.08f, 4.52f } },
			{ "realDrums", new float[] { .21f, .46f, .77f, 1.85f, 3.08f, 4.52f } },
			{ "ghDrums", new float[] { .21f, .46f, .77f, 1.85f, 3.08f, 4.52f } },
			{ "vocals", new float[] { 4f*0.05f, 4f*0.11f, 4f*0.19f, 4f*0.46f, 4f*0.77f, 4f*1.06f } },
			{ "harmVocals", new float[] { 4f*0.05f, 4f*0.11f, 4f*0.19f, 4f*0.46f, 4f*0.77f, 4f*1.06f } }
		};

		// keep track of all instances to calculate the band total
		public static List<StarScoreKeeper> instances = new();
		public static double TotalMax {
			get {
				double sum = 0;
				foreach (var ins in instances) {
					sum += ins.baseScore;
				}
				return sum;
			}
		}

		/// <summary>
		/// Average of all stars earned by each instance in the currently playing band.
		/// </summary>
		public static double BandStars {
			get {
				if (instances.Count > 0)
					return instances.Average(ins => ins.Stars);
				return 0;
			}
		}

		public static void Reset() {
			Debug.Log("Clearing StarKeeper instances!");
			instances.Clear();
		}

		private ScoreKeeper scoreKeeper;

		/// <summary>
		/// The maximum score achievable at 1x multiplier.
		/// </summary>
		public double baseScore { get; private set; }

		/// <summary>
		/// Minimum points needed to get 1, 2, 3, 4, 5, and gold stars respectively.
		/// </summary>
		public double[] scoreThreshold;

		/// <summary>
		/// How many stars currently earned.
		/// </summary>
		public double Stars {
			get {
				int stars = 5;
				while (stars >= 0 && scoreKeeper.score < scoreThreshold[stars]) { --stars; }
				stars += 1; // stars earned, also index of threshold for next star

				switch (stars) {
					case int s when s == 0:
						return scoreKeeper.score / scoreThreshold[s];
					case int s when s <= 5:
						return (double) s + (scoreKeeper.score - scoreThreshold[s - 1]) / (scoreThreshold[s] - scoreThreshold[s - 1]);
					default: // 6+ stars
						return (double) 5 + (scoreKeeper.score - scoreThreshold[4]) / (scoreThreshold[5] - scoreThreshold[4]);
				}
			}
		}

	public StarScoreKeeper(List<NoteInfo> chart, ScoreKeeper scoreKeeper, string instrument, int ptPerNote, double ptSusPerBeat = 0) {
			instances.Add(this);
			this.scoreKeeper = scoreKeeper;

			// calculate and store base score
			baseScore = 0;
			foreach (var note in chart) {
				baseScore += ptPerNote;
				if (note.length > .2f) {
					baseScore += ptSusPerBeat * Util.Utils.InfoLengthInBeats(note, Play.Instance.chart.beats);
				}
			}

			// populate scoreThreshold
			scoreThreshold = new double[] {
				instrumentThreshold[instrument][0] * baseScore,
				instrumentThreshold[instrument][1] * baseScore,
				instrumentThreshold[instrument][2] * baseScore,
				instrumentThreshold[instrument][3] * baseScore,
				instrumentThreshold[instrument][4] * baseScore,
				instrumentThreshold[instrument][5] * baseScore
			};
		}
	}
}