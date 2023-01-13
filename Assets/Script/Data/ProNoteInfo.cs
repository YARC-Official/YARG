namespace YARG.Data {
	public class ProNoteInfo {
		public float time;
		public float length;

		public int[] frets;

		public float EndTime => time + length;

		public ProNoteInfo(float time, float length, int[] frets) {
			this.time = time;
			this.length = length;
			this.frets = frets;
		}
	}
}