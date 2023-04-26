using System.Collections.Generic;
using MoonscraperChartEditor.Song;

namespace YARG.Chart {
	public interface IChartLoader<T> {
		
		public List<T> GetNotesFromChart(MoonSong song, MoonChart chart);
		
	}
}
