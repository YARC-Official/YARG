namespace YARG.Data {
	public class EventInfo {
		public string name;

		public float time;
		public float length;

		public float EndTime => time + length;

		public EventInfo(string name, float time, float length = 0f) {
			this.time = time;
			this.name = name;
			this.length = length;
		}
	}
}