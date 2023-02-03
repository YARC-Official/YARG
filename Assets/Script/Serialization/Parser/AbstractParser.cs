using YARG.Data;

namespace YARG.Serialization.Parser {
	public abstract class AbstractParser {
		protected string[] files;
		protected float delay;

		public AbstractParser(string[] files, float delay) {
			this.files = files;
			this.delay = delay;
		}

		public abstract void Parse(Chart chart);
	}
}