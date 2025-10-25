using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YARG.Song
{
    public static partial class Genrelizer
    {
        [Serializable]
        // This is a serialized class; naming conventions are JSON's, not C#'s
        [SuppressMessage("ReSharper", "All")]
        private class GenreMappingData
        {
            public string genre;
            public string name;

            public Dictionary<string, List<string>> substitutions = new();
            public List<string> prefixes = new();
            public List<string> suffixes = new();
            public Dictionary<string, string> localizations = new();

            public Dictionary<string, SubgenreMappingData> subgenres = new();
        }

        [Serializable]
        // This is a serialized class; naming conventions are JSON's, not C#'s
        [SuppressMessage("ReSharper", "All")]
        private class SubgenreMappingData
        {
            public string genre;

            public List<string> prefixes = new();
            public List<string> suffixes = new();
            public Dictionary<string, List<string>> substitutions = new();
            public Dictionary<string, string> localizations = new();
        }
    }
}
