using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using YARG.Data;
using YARG.Util;

namespace YARG.PlayMode {
	/// <summary>
	/// Star-score tracking. Could probably be combined with ScoreKeeper.
	/// </summary>
	public class StarScoreKeeper {
		// https://github.com/hmxmilohax/Rock-Band-4-Deluxe/blob/0f1562bcf838b82bac0f9bdd8e6193152a73ae88/_rivals_ark/ps4/config/include/star_thresholds.dta
		/// <summary>
		/// Minimum avg. multipliers to get 1, 2, 3, 4, 5, and gold stars respectively.
		/// </summary>
		public static readonly float[] starThresholdsDefault = { .21f, .46f, .77f, 1.85f, 3.08f, 4.52f };

		/// <summary>
		/// Minimum avg. multipliers to get 1, 2, 3, 4, 5, and gold stars on Bass respectively.
		/// </summary>
		public static readonly float[] starThresholdsBass = { .21f, .5f, .9f, 2.77f, 4.62f, 6.78f };

		/// <summary>
		/// Minimum avg. multipliers to get 1, 2, 3, 4, 5, and gold stars on Drums respectively.
		/// </summary>
		public static readonly float[] starThresholdsDrums = { .21f, .46f, .77f, 1.85f, 3.08f, 4.29f };

		/// <summary>
		/// Minimum avg. multipliers to get 1, 2, 3, 4, 5, and gold stars on Vocals respectively.
		/// </summary>
		public static readonly float[] starThresholdsVocals = { .21f, .46f, .77f, 1.85f, 3.08f, 4.18f };

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
		public double[] scoreThresholds;

		/// <summary>
		/// How many stars currently earned.
		/// </summary>
		public double Stars {
			get {
				int stars = 5;
				while (stars >= 0 && scoreKeeper.Score < scoreThresholds[stars]) { --stars; }
				stars += 1; // stars earned, also index of threshold for next star

				switch (stars) {
					case int s when s == 0:
						return scoreKeeper.Score / scoreThresholds[s];
					case int s when s <= 5:
						return (double) s + (scoreKeeper.Score - scoreThresholds[s - 1]) / (scoreThresholds[s] - scoreThresholds[s - 1]);
					default: // 6+ stars
						return (double) 5 + (scoreKeeper.Score - scoreThresholds[4]) / (scoreThresholds[5] - scoreThresholds[4]);
				}
			}
		}

		public StarScoreKeeper(List<NoteInfo> chart, ScoreKeeper scoreKeeper, string instrument, int ptPerNote = 25, double ptSusPerBeat = 0) {
			instances.Add(this);
			this.scoreKeeper = scoreKeeper;

			// solo sections
			List<EventInfo> soloEvents = new();
			foreach (var ev in Play.Instance.chart.events) {
				if (ev.name == $"solo_{instrument}") {
					soloEvents.Add(ev);
				}
			}	

			// calculate and store base score
			BaseScore = 0;
			foreach (var note in chart) {
				BaseScore += ptPerNote;
				if (note.length > .2f) {
					BaseScore += ptSusPerBeat * Utils.InfoLengthInBeats(note, Play.Instance.chart.beats);
				}

				// check if note is in a solo section
				foreach (var ev in soloEvents) {
					if (ev.time <= note.time && note.time < ev.EndTime) {
						// solo notes get double score, effectively
						BaseScore += ptPerNote;
						break;
					}
				}
			}

			SetupScoreThreshold(instrument);
		}

		public StarScoreKeeper(ScoreKeeper scoreKeeper, string instrument, int noteCount, int ptPerNote, int soloNotes = 0) {
			instances.Add(this);
			this.scoreKeeper = scoreKeeper;

			// solo notes get double score, effectively
			BaseScore = (noteCount + soloNotes) * ptPerNote;

			SetupScoreThreshold(instrument);
		}

		// populate scoreThreshold
		private void SetupScoreThreshold(string instrument) {
			float[] curThresholds;
			switch (instrument) {
				case var i when i.ToLower().Contains("bass"):
					curThresholds = starThresholdsBass;
					break;
				case var i when i.ToLower().Contains("drum"):
					curThresholds = starThresholdsDrums;
					break;
				case var i when i.ToLower().Contains("vocal"):
					curThresholds = starThresholdsVocals;
					break;
				default:
					curThresholds = starThresholdsDefault;
					break;
			}
			scoreThresholds = (from mul in curThresholds select mul * BaseScore).ToArray();

			// Debug.Log(instrument);
			// Debug.Log($"Base Score: {BaseScore}");
			// Debug.Log($"Star Reqs: {string.Join(", ", scoreThresholds)}");
		}
	}
}