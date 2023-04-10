using System.Collections.Generic;

namespace YARG.Data {
	public class NoteInfo : AbstractInfo {
		/// <value>
		/// Acts as the button for a five fret, or drum pad for drums.
		/// </value>
		public int fret;
		/// <summary>
		/// Hammer-on/pull-off, or Cymbal for drums.
		/// </summary>
		public bool hopo;

		/// <summary>
		/// Whether or not this HOPO is automatic.<br/>
		/// Used for difficulty downsampling.
		/// </summary>
		public bool autoHopo;

		/// <value>
		/// The fret numbers for a pro-guitar.
		/// </value>
		public int[] stringFrets;
		/// <summary>
		/// Pro-guitar mute note.
		/// </summary>
		public bool muted;

		/// <summary>
		/// Returns the maximum sustain points possible for this note.
		/// Does not include initial strum points.
		/// </summary>
		/// <param name="beats">Beat times from this note's chart.</param>
		/// <returns></returns>
		public double MaxSustainPoints(List<float> beats) {
			int beatIndex = 1;
			// set beatIndex to first relevant beat
			for (int i = beatIndex; i < beats.Count; ++i) {
				if (beats[i] > time) {
					beatIndex = i;
					break;
				}
			}

			double points = 0;
			// add segments of the sustain wrt tempo
			for (; beatIndex < beats.Count && beats[beatIndex] <= EndTime; ++beatIndex) {
				var curBPS = 1/(beats[beatIndex] - beats[beatIndex - 1]);
				// Unit math: pt/b * s * b/s = pt
				points += 12.0 * (beats[beatIndex] - time) * curBPS;
			}

			// calculate final segment where EndTime is between two beats (beatIndex-1 and beatIndex)
			if (beatIndex > EndTime) {
				var curBPS = 1/(beats[beatIndex] - beats[beatIndex - 1]);
				points += 12.0 * (EndTime - beats[beatIndex - 1]) * curBPS;
			}
			return points;
		}

		public NoteInfo Duplicate() {
			return (NoteInfo) MemberwiseClone();
		}
	}
}