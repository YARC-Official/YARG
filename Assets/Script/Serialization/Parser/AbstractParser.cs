using YARG.Data;
using YARG.Song;

namespace YARG.Serialization.Parser {
	public abstract class AbstractParser {
		protected SongEntry songEntry;
		protected string[] files;

		public AbstractParser(SongEntry songEntry, string[] files) {
			this.songEntry = songEntry;
			this.files = files;
		}

		public abstract void Parse(YargChart yargChart);
	}
}