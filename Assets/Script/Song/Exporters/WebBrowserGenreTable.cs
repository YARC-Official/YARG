using System.Collections.Generic;

namespace YARG.Song.Exporters
{
    /// <summary>
    /// Genre interner for compact web export.
    /// Collects unique genre strings and assigns each an integer index,
    /// reducing JSON payload size vs repeating full strings.
    /// </summary>
    public class WebBrowserGenreTable
    {
        private readonly List<string> _genres = new();
        private readonly Dictionary<string, int> _index = new();

        /// <summary>
        /// Gets the read-only list of genres in insertion order.
        /// </summary>
        public IReadOnlyList<string> Genres => _genres;

        /// <summary>
        /// Interns a genre string, returning its index or null if empty.
        /// First occurrence adds to the table and returns the new index.
        /// Subsequent occurrences return the existing index.
        /// </summary>
        /// <param name="genre">The genre string to intern (may be null/empty).</param>
        /// <returns>
        /// The assigned index (0-based) if non-empty after stripping,
        /// null if the input is null or empty/whitespace.
        /// </returns>
        /// <remarks>
        /// Strips whitespace to normalize variations like "Rock " vs "Rock".
        /// Empty strings return null so the JSON record omits the 'g' field.
        /// </remarks>
        public int? Intern(string genre)
        {
            // Null or empty/whitespace -> omit the field.
            if (string.IsNullOrWhiteSpace(genre))
            {
                return null;
            }

            // Strip whitespace to normalize.
            string normalized = genre.Trim();

            // Check if already interned.
            if (_index.TryGetValue(normalized, out int existingIndex))
            {
                return existingIndex;
            }

            // First occurrence: add to table.
            int newIndex = _genres.Count;
            _genres.Add(normalized);
            _index[normalized] = newIndex;
            return newIndex;
        }
    }
}
