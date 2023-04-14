using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

namespace YARG.Util {
	public static class Utils {
		/// <summary>
		/// Checks if the path <paramref name="a"/> is equal to the path <paramref name="b"/>.<br/>
		/// Platform specific case sensitivity is taken into account.
		/// </summary>
		public static bool PathsEqual(string a, string b) {
#if UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX
			
			// Linux is case sensitive
			return Path.GetFullPath(a).Equals(Path.GetFullPath(b), StringComparison.CurrentCulture);
			
#else

			// Windows and OSX are not case sensitive
			return Path.GetFullPath(a).Equals(Path.GetFullPath(b), StringComparison.CurrentCultureIgnoreCase);

#endif
		}

		/// <summary>
		/// Calculates the length of an Info object in beats.
		/// </summary>
		/// <param name="beatTimes">List of beat times associated with the Info object.</param>
		/// <returns>Length of the Info object in beats.</returns>
		public static float InfoLengthInBeats(YARG.Data.AbstractInfo info, List<float> beatTimes) {
			int beatIndex = 1;
			// set beatIndex to first relevant beat
			while (beatIndex < beatTimes.Count && beatTimes[beatIndex] <= info.time) {
				++beatIndex;
			}

			float beats = 0;
			// add segments of the length wrt tempo
			for (; beatIndex < beatTimes.Count && beatTimes[beatIndex] <= info.EndTime; ++beatIndex) {
				var curBPS = 1/(beatTimes[beatIndex] - beatTimes[beatIndex - 1]);
				// Unit math: s * b/s = pt
				beats += (beatTimes[beatIndex] - Mathf.Max(beatTimes[beatIndex - 1], info.time)) * curBPS;
			}

			// segment where EndTime is between two beats (beatIndex-1 and beatIndex)
			if (beatIndex < beatTimes.Count && beatTimes[beatIndex-1] < info.EndTime && info.EndTime < beatTimes[beatIndex]) {
				var bps = 1/(beatTimes[beatIndex] - beatTimes[beatIndex - 1]);
				beats += (info.EndTime - beatTimes[beatIndex - 1]) * bps;
			}
			// segment where EndTime is BEYOND the final beat
			else if (info.EndTime > beatTimes[^1]) {
				var bps = 1/(beatTimes[^1] - beatTimes[^2]);
				var toAdd = (info.EndTime - beatTimes[^1]) * bps;
				beats += toAdd;
			}

			return beats;
		}
	}
}
