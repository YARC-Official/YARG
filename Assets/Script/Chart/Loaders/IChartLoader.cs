using System.Collections.Generic;
using MoonscraperChartEditor.Song;
using YARG.Data;

namespace YARG.Chart {
	public interface IChartLoader<T> {
		
		public List<T> GetNotesFromChart(MoonSong song, Difficulty chart);
		
	}
}
