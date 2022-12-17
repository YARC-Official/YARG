namespace YARG.Serialization {
	public abstract class AbstractParser {
		protected string file;

		public AbstractParser(string file) {
			this.file = file;
		}

		public abstract void Parse(Chart chart);
	}
}