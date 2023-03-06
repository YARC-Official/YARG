using System.Collections.Generic;

namespace YARG.Data {
	public class LyricInfo : AbstractInfo {
		public string lyric;
		public bool inharmonic;

		public List<(float, (float note, int octave))> pitchOverTime;
	}
}