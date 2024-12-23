using MoonscraperChartEditor.Song;

namespace YARG.Core.UnitTests.Parsing
{
    public class MoonSongLoaderTests
    {
        public const uint RESOLUTION = 192;
        public const float TEMPO = 120;
        public const double SECONDS_PER_BEAT = 60 / TEMPO;

        public static double SECONDS(double beat) => SECONDS_PER_BEAT * beat;
        public static uint TICKS(double beat) => (uint) (RESOLUTION * beat);

        internal static MoonSong CreateSong()
        {
            return new MoonSong(RESOLUTION);
        }
    }
}