namespace YARG {
	public class NoteInfo {
		public float time;
		public float length;

		public bool hopo;
		public int fret;

		public NoteInfo(float time, int fret, float length, bool hopo) {
			this.time = time;
			this.fret = fret;
			this.length = length;
			this.hopo = hopo;
		}
	}
}