using System.Collections.Generic;
using Unity.Mathematics;

namespace YARG.PlayMode {
	// Score keeping for each track with an instance
	public class ScoreKeeper {
		public delegate void ScoreAction();
		/// <summary>
		/// Fires when points have been added to an instance's score.
		/// </summary>
		public static event ScoreAction OnScoreChange;

		// keep track of all instances to calculate the band total
		public static List<ScoreKeeper> instances = new();
		public static double TotalScore {
			get {
				double sum = 0;
				foreach (var ins in instances) {
					sum += ins.Score;
				}
				return sum;
			}
		}

		public static void Reset() {
			instances.Clear();
		}

		public double Score { get; private set; } = 0;

		/// <summary>
		/// Add points for this keeper. Fires the OnScoreChange event.
		/// </summary>
		/// <param name="points"></param>
		public void Add(double points) {
			Score += points;
			OnScoreChange?.Invoke();
		}

		/// <summary>
		/// Calculate and store bonus points earned from a solo section.
		/// </summary>
		/// <param name="notesHit"></param>
		/// <param name="notesMax"></param>
		/// <returns>The bonus points earned.</returns>
		public double AddSolo(int notesHit, int notesMax) {
			double ratio = (double) notesHit / notesMax;

			if (ratio < 0.6)
				return 0;

			// linear
			double multiplier = math.clamp((ratio - 0.6) / 0.4, 0, 1);
			double ptsEarned = 100 * notesHit * multiplier;

			// +5% bonus points
			// TODO: limit to FC? decide to keep at all?
			if (ratio >= 1)
				ptsEarned = 1.05 * ptsEarned;

			Add(ptsEarned);
			return ptsEarned;
		}

		public ScoreKeeper() {
			instances.Add(this);
		}
	}
}
