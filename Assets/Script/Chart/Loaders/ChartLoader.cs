using System.Collections.Generic;
using MoonscraperChartEditor.Song;
using YARG.Data;

namespace YARG.Chart {
	public abstract class ChartLoader<T> {
		public MoonSong.MoonInstrument Instrument { get; protected set; }
		public string InstrumentName { get; protected set; }
		public Difficulty MaxDifficulty { get; protected set; } = Difficulty.EXPERT;

		public abstract List<T> GetNotesFromChart(MoonSong song, Difficulty chart);
	}
}
