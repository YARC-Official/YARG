namespace YARG.Data {
	public abstract class AbstractInfo {
		public float time;
		public float length;

		public float EndTime => time + length;
	}
}