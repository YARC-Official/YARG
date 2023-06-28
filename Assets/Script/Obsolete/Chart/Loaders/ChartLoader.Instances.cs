using MoonscraperChartEditor.Song;

namespace YARG.Chart
{
    public static class ChartLoader
    {
        public static readonly GuitarChartLoader GuitarLoader = new(MoonSong.MoonInstrument.Guitar);
        public static readonly GuitarChartLoader GuitarCoopLoader = new(MoonSong.MoonInstrument.GuitarCoop);
        public static readonly GuitarChartLoader RhythmLoader = new(MoonSong.MoonInstrument.Rhythm);
        public static readonly GuitarChartLoader BassLoader = new(MoonSong.MoonInstrument.Bass);
        public static readonly GuitarChartLoader KeysLoader = new(MoonSong.MoonInstrument.Keys);

        public static readonly FourLaneDrumsChartLoader DrumsLoader = new(pro: false);
        public static readonly FourLaneDrumsChartLoader ProDrumsLoader = new(pro: true);
        public static readonly FiveLaneDrumsChartLoader FiveLaneDrumsLoader = new();
    }
}