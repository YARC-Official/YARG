using System.Collections.Generic;
using MoonscraperChartEditor.Song;
using YARG.Data;

namespace YARG.Chart {
	public abstract class ChartLoader<T> {
		public abstract List<T> GetNotesFromChart(MoonSong song, Difficulty chart);
	}
}
