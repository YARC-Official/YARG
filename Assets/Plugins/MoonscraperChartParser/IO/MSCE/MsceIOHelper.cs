using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace MoonscraperChartEditor.Song.IO
{
    // Stores space characters found in ChartEvent objects as Japanese full-width spaces. Need to convert this back when loading.
    public class MsceIOHelper
    {
        public const string FileExtention = ".msce";

        public static readonly Dictionary<char, char> LocalEventCharReplacementToMsce = new Dictionary<char, char>()
        {
            { ' ', '\u3000' }
        };

        public static readonly Dictionary<char, char> LyricEventCharReplacementToMsce = new Dictionary<char, char>()
        {
            { '\"', '`' }
        };

        public static readonly Dictionary<char, char> LocalEventCharReplacementFromMsce = LocalEventCharReplacementToMsce.ToDictionary((i) => i.Value, (i) => i.Key);
        public static readonly Dictionary<char, char> LyricEventCharReplacementFromMsce = LyricEventCharReplacementToMsce.ToDictionary((i) => i.Value, (i) => i.Key);
    }
}
