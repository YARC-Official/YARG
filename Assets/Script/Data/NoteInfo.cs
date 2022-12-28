namespace YARG.Data {
	public class NoteInfo {
		public float time;
		public float length;

		public int fret;
		public bool hopo;

		public NoteInfo(float time, float length, int fret, bool hopo) {
			this.time = time;
			this.length = length;
			this.fret = fret;
			this.hopo = hopo;
		}
	}
}