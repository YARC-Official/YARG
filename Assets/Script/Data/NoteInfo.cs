using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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
			while (beatIndex < beats.Count && beats[beatIndex] <= time) {
				++beatIndex;
			}

			double points = 0;
			// add segments of the sustain wrt tempo
			for (; beatIndex < beats.Count && beats[beatIndex] <= EndTime; ++beatIndex) {
				var curBPS = 1/(beats[beatIndex] - beats[beatIndex - 1]);
				// Unit math: pt/b * s * b/s = pt
				points += 12.0 * (beats[beatIndex] - Mathf.Max(beats[beatIndex - 1], time)) * curBPS;
			}

			// segment where EndTime is between two beats (beatIndex-1 and beatIndex)
			if (beatIndex < beats.Count && beats[beatIndex-1] < EndTime && EndTime < beats[beatIndex]) {
				var bps = 1/(beats[beatIndex] - beats[beatIndex - 1]);
				points += 12.0 * (EndTime - beats[beatIndex - 1]) * bps;
			}
			// segment where EndTime is BEYOND the song's final beat
			else if (EndTime > beats[^1]) {
				var bps = 1/(beats[^1] - beats[^2]);
				var toAdd = 12.0 * (EndTime - beats[^1]) * bps;
				points += toAdd;
			}

			return points;
		}

		public NoteInfo Duplicate() {
			return (NoteInfo) MemberwiseClone();
		}
	}
}