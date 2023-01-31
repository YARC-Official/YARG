using System.Collections.Generic;

namespace YARG.Data {
	public class GenericLyricInfo {
		public float time;
		public List<(float time, string word)> lyric;

		public GenericLyricInfo(float time, List<(float, string)> lyric) {
			this.time = time;
			this.lyric = lyric;
		}
	}
}