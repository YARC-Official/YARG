using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace YARG.PlayMode {
	public class PerformanceTextSizer {
		// Used to set the keyframes of the animation
		public float fontSize { get; set; }
		public float animTimeLength { get; set; }
		public float animTimeRemaining { get; set; }

		// Set whether fontSize represents the "peak" or the "rest" size
		// Note that rest size == peak size * 0.9f for animation purposes
		public bool representsPeakSize { get; set; }

		public PerformanceTextSizer(float fs, float atl, bool rps = false) {
	        fontSize = fs;
	        animTimeLength = atl;
	        animTimeRemaining = 0.0f;
	        representsPeakSize = rps;
	    }

		/// <returns>
		/// The font size of the text given the current animation timestamp
		/// </returns>
	    /// <summary>
	    /// Given an animTimeLenth "a":
		/// - At t = 0s, start from 0% size and start the SHARP climb up to 100% size
		/// - At t = 1/6s, be at 100% size and start the fall down to 90% size
		/// - At t = 1/3s, rest at 90% size
		/// - At t = (a - 1/3)s, start from 90% size and star the climb back up to 100% size
		/// - At t = (a - 1/6)s. be 100% size and start the SHARP fall back down to 0% size
		/// - At t = (a)s, rest at 0% size
		/// a = 1s for vocal performance text (e.g., AWESOME, STRONG, AWFUL)
		/// a = 3s for guitar / drums / keys performance text (e.g., BASS GROOVE, HOT START, STRONG FINISH)
		/// Anim should be symmetrical with respect to the halfway point to prevent snapping, even if under 2/3s
	    /// </summary>
		public float PerformanceTextFontSize() {
			// Define the relative size before font sizing is applied
			float relativeSize = 0.0f;
			float halfwayPoint = animTimeLength / 2.0f;

			// Determine the text size relative to its countdown timestamp
			if ((animTimeRemaining > animTimeLength) || (animTimeRemaining < 0.0f)) {
				relativeSize = 0.0f;
			} else if (animTimeRemaining >= halfwayPoint) {
				if (animTimeRemaining > (animTimeLength - 0.16666f)) {
					relativeSize = (-1290.0f * Mathf.Pow(
						animTimeRemaining - animTimeLength + 0.16666f, 4.0f)) + 0.9984f;
				} else {
					float denominator = 1.0f + Mathf.Pow((float) Math.E,
						-50.0f * (animTimeRemaining - animTimeLength + 0.25f));
					relativeSize = (0.1f / denominator) + 0.9f;
				}
			} else if (animTimeRemaining < halfwayPoint) {
				if (animTimeRemaining < 0.16666f) {
					relativeSize = (-1290.0f * Mathf.Pow(
						animTimeRemaining - 0.16666f, 4.0f)) + 0.9984f;
				} else {
					float denominator = 1.0f + Mathf.Pow((float) Math.E,
						-50.0f * (animTimeRemaining - 0.25f));
					relativeSize = (-0.1f / denominator) + 1.0f;
				}
			}

			// Size appropriately
			return relativeSize * fontSize * (representsPeakSize ? 1.0f : 1.11111f);
		}
	}
}