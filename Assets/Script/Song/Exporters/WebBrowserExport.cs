using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using YARG.Core.Song;

namespace YARG.Song.Exporters
{
    /// <summary>
    /// Web song browser export generator.
    /// Streams the in-memory library into a self-contained HTML file
    /// using the shared template (WebBrowserTemplate) and compact records
    /// (WebBrowserRecord). The file is written segment-by-segment so the
    /// full HTML document is never materialized in memory.
    /// </summary>
    public static class WebBrowserExport
    {
        /// <summary>
        /// Exports the current song library to a self-contained HTML file.
        /// </summary>
        /// <param name="path">The destination file path.</param>
        /// <remarks>
        /// Mirrors OuvertExport.Export signature for integration with
        /// FileExplorerHelper.OpenSaveFile callback pattern.
        /// </remarks>
        public static void Export(string path)
        {
            // Build data: genres table and sorted records.
            var genres = new WebBrowserGenreTable();
            var records = WebBrowserRecord.BuildAll(SongContainer.Songs, genres);

            // Configure serializer: omit null/empty fields (e.g., empty album, missing year).
            var settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            };

            // Stream to file: UTF-8, no BOM (HTML/JS files don't need BOM).
            using var stream = File.Create(path);
            using var writer = new StreamWriter(stream, new UTF8Encoding(false));

            // Write template segment 0 (before /*DATA*/).
            writer.Write(WebBrowserTemplate.Seg0);

            // DATA: a raw JSON array literal (JSON is a subset of JS). Escape '<' to \u003C so a
            // "</script>" or "<!--" inside user-controlled text (artist/title/album) cannot
            // terminate the inline <script> block. JSON has no structural '<' (only inside string
            // values), and the JS engine decodes \u003C back to '<' when parsing the literal.
            writer.Write(JsonConvert.SerializeObject(records, settings).Replace("<", "\\u003C"));

            // Write template segment 1 (between /*DATA*/ and /*GENRES*/).
            writer.Write(WebBrowserTemplate.Seg1);

            // GENRES: a raw JSON array literal (not string-wrapped), with the same '<' escape
            // since genre strings are also user-controlled song metadata.
            writer.Write(JsonConvert.SerializeObject(genres.Genres, settings).Replace("<", "\\u003C"));

            // Write template segment 2 (between /*GENRES*/ and /*META*/).
            writer.Write(WebBrowserTemplate.Seg2);

            // META: a raw JSON object literal (source/generated/count are not user-controlled).
            // Local date, not UTC — this is a user-facing "Generated ..." footer.
            var meta = new
            {
                source = "YARG",
                generated = DateTime.Now.ToString("yyyy-MM-dd"),
                count = records.Count
            };
            writer.Write(JsonConvert.SerializeObject(meta, settings));

            // Write template segment 3 (after /*META*/).
            writer.Write(WebBrowserTemplate.Seg3);
        }
    }
}
