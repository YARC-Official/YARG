using MoonscraperChartEditor.Song;

public static class SongIniFunctions
{

    static string GetCHDifficultyTagForInstrument(MoonSong.MoonInstrument moonInstrument)
    {
        switch (moonInstrument)
        {
            case MoonSong.MoonInstrument.Guitar:
                return "diff_guitar";

            case MoonSong.MoonInstrument.Rhythm:
                return "diff_rhythm";

            case MoonSong.MoonInstrument.Bass:
                return "diff_bass";

            case MoonSong.MoonInstrument.Drums:
                return "diff_drums";

            case MoonSong.MoonInstrument.Keys:
                return "diff_keys";

            case MoonSong.MoonInstrument.GHLiveGuitar:
                return "diff_guitarghl";

            case MoonSong.MoonInstrument.GHLiveBass:
                return "diff_bassghl";

            case MoonSong.MoonInstrument.GHLiveRhythm:
                return "diff_rhythmghl";
        }

        return string.Empty;
    }
}
