using System.Collections.Generic;
using MoonscraperChartEditor.Song;

public static class LyricHelper
{
    public const string LYRIC_EVENT_PREFIX = "lyric ";
    public const string PhraseStartText = "phrase_start";
    public const string PhraseEndText = "phrase_end";

    public static readonly Dictionary<string, string> CloneHeroCharSubstitutions = new Dictionary<string, string>()
    {     
        { "\"", "`" },

        /*
        { "-", "=" },
        { " ", "_" },
        { "#", string.Empty },
        { "^", string.Empty },
        { "/", string.Empty },
        { "+", string.Empty }, 
        { "%", string.Empty },
        */
    };

    public static bool IsLyric(this Event e)
    {
        return e.classID == (int)SongObject.ID.Event && IsLyric(e.title);
    }

    public static bool IsLyric(string title)
    {
        return title.StartsWith(LYRIC_EVENT_PREFIX);
    }
}
