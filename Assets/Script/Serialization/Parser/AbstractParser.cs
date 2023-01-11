using YARG.Data;

namespace YARG.Serialization.Parser {
	public abstract class AbstractParser {
		protected string file;
		protected float delay;

		public AbstractParser(string file, float delay) {
			this.file = file;
			this.delay = delay;
		}

		public abstract void Parse(Chart chart);
	}
}