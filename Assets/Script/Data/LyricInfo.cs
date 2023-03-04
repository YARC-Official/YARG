namespace YARG.Data {
	public class LyricInfo : AbstractInfo {
		public string lyric;
		public bool inharmonic;

		public LyricInfo(float time, float length, string lyric, bool inharmonic) {
			this.time = time;
			this.length = length;
			this.lyric = lyric;
			this.inharmonic = inharmonic;
		}
	}
}