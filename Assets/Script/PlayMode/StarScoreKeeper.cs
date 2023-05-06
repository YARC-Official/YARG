using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using YARG.Data;

namespace YARG.PlayMode {
	/// <summary>
	/// Star-score tracking. Could probably be combined with ScoreKeeper.
	/// </summary>
	public class StarScoreKeeper {
		/// <summary>
		/// Minimum avg. multipliers to get 1, 2, 3, 4, 5, and gold stars respectively.
		/// </summary>
		public static readonly Dictionary<string, float[]> instrumentThreshold = new() {
			{ "guitar", new float[] { .21f, .46f, .77f, 1.85f, 3.08f, 4.52f } },
			{ "bass", new float[] { .21f, .5f, .9f, 2.77f, 4.62f, 6.78f } },
			{ "keys", new float[] { .21f, .46f, .77f, 1.85f, 3.08f, 4.52f } },
			{ "guitarCoop", new float[] { .21f, .46f, .77f, 1.85f, 3.08f, 4.52f } },
			{ "rhythm", new float[] { .21f, .46f, .77f, 1.85f, 3.08f, 4.52f } },
			{ "realGuitar", new float[] { .21f, .46f, .77f, 1.85f, 3.08f, 4.52f } },
			{ "realBass", new float[] { .21f, .46f, .77f, 1.85f, 3.08f, 4.52f } },
			{ "drums", new float[] { .21f, .46f, .77f, 1.85f, 3.08f, 4.52f } },
			{ "realDrums", new float[] { .21f, .46f, .77f, 1.85f, 3.08f, 4.52f } },
			{ "ghDrums", new float[] { .21f, .46f, .77f, 1.85f, 3.08f, 4.52f } },
			{ "vocals", new float[] { 4f*0.05f, 4f*0.11f, 4f*0.19f, 4f*0.46f, 4f*0.77f, 4f*1.06f } },
			{ "harmVocals", new float[] { 4f*0.05f, 4f*0.11f, 4f*0.19f, 4f*0.46f, 4f*0.77f, 4f*1.06f } }
		};

		// keep track of all instances in Play to calculate the band total
		public static List<StarScoreKeeper> instances = new();

		/// <summary>
		/// Average of all stars earned by each instance in the currently playing band.
		/// </summary>
		public static double BandStars {
			get {
				// seems like players with no parts get NaN stars due to divide by 0
				var tmp = from ins in instances where !Double.IsNaN(ins.Stars) select ins.Stars;
				if (tmp.Count() > 0) {
					return tmp.Average();
				}
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
		public double BaseScore { get; private set; }

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
				while (stars >= 0 && scoreKeeper.Score < scoreThreshold[stars]) { --stars; }
				stars += 1; // stars earned, also index of threshold for next star

				switch (stars) {
					case int s when s == 0:
						return scoreKeeper.Score / scoreThreshold[s];
					case int s when s <= 5:
						return (double) s + (scoreKeeper.Score - scoreThreshold[s - 1]) / (scoreThreshold[s] - scoreThreshold[s - 1]);
					default: // 6+ stars
						return (double) 5 + (scoreKeeper.Score - scoreThreshold[4]) / (scoreThreshold[5] - scoreThreshold[4]);
				}
			}
		}

		public StarScoreKeeper(List<NoteInfo> chart, ScoreKeeper scoreKeeper, string instrument, int ptPerNote = 25, double ptSusPerBeat = 0) {
			instances.Add(this);
			this.scoreKeeper = scoreKeeper;

			// calculate and store base score
			BaseScore = 0;
			foreach (var note in chart) {
				BaseScore += ptPerNote;
				if (note.length > .2f) {
					BaseScore += ptSusPerBeat * Util.Utils.InfoLengthInBeats(note, Play.Instance.chart.beats);
				}
			}

			SetupScoreThreshold(instrument);
		}

		public StarScoreKeeper(ScoreKeeper scoreKeeper, string instrument, int noteCount, int ptPerNote) {
			instances.Add(this);
			this.scoreKeeper = scoreKeeper;

			BaseScore = noteCount * ptPerNote;

			SetupScoreThreshold(instrument);
		}

		// populate scoreThreshold
		private void SetupScoreThreshold(string instrument) {
			scoreThreshold = new double[] {
				instrumentThreshold[instrument][0] * BaseScore,
				instrumentThreshold[instrument][1] * BaseScore,
				instrumentThreshold[instrument][2] * BaseScore,
				instrumentThreshold[instrument][3] * BaseScore,
				instrumentThreshold[instrument][4] * BaseScore,
				instrumentThreshold[instrument][5] * BaseScore
			};
		}
	}
}