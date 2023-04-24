using YARG.Data;

namespace YARG.Serialization.Parser {
	public abstract class AbstractParser {
		protected SongInfo songInfo;
		protected string[] files;

		public AbstractParser(SongInfo songInfo, string[] files) {
			this.songInfo = songInfo;
			this.files = files;
		}

		public abstract void Parse(YargChart yargChart);
	}
}