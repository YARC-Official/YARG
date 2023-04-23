using MoonscraperChartEditor.Song;

public static class SongIniFunctions
{

    static string GetCHDifficultyTagForInstrument(Song.Instrument instrument)
    {
        switch (instrument)
        {
            case Song.Instrument.Guitar:
                return "diff_guitar";

            case Song.Instrument.Rhythm:
                return "diff_rhythm";

            case Song.Instrument.Bass:
                return "diff_bass";

            case Song.Instrument.Drums:
                return "diff_drums";

            case Song.Instrument.Keys:
                return "diff_keys";

            case Song.Instrument.GHLiveGuitar:
                return "diff_guitarghl";

            case Song.Instrument.GHLiveBass:
                return "diff_bassghl";

            case Song.Instrument.GHLiveRhythm:
                return "diff_rhythmghl";
        }

        return string.Empty;
    }
}
