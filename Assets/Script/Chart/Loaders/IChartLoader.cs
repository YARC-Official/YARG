using System.Collections.Generic;
using MoonscraperChartEditor.Song;

namespace YARG.Chart {
	public interface IChartLoader<T> where T : Note {
		
		public List<T> GetNotesFromChart(MoonSong song, MoonChart chart);
		
	}
}
