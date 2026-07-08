using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using YARG.Core;
using YARG.Core.Song;
using YARG.Core.Utility;

namespace YARG.Song.Exporters
{
    /// <summary>
    /// Compact song record for web browser export.
    /// JSON property names are single-character to minimize payload size.
    /// Plain mutable class (NOT record/init) so it compiles under Unity's C# runtime,
    /// which lacks the C# 9 IsExternalInit shim. Constructed once via object initializer,
    /// then serialized immediately — no shared mutable state.
    /// </summary>
    public class WebBrowserRecord
    {
        /// <summary>Artist name (stripped of rich text).</summary>
        [JsonProperty("a")]
        public string Artist { get; set; }

        /// <summary>Song title (stripped of rich text).</summary>
        [JsonProperty("t")]
        public string Title { get; set; }

        /// <summary>Album name (stripped of rich text), omitted if empty/null.</summary>
#nullable enable
        [JsonProperty("al", NullValueHandling = NullValueHandling.Ignore)]
        public string? Album { get; set; }
#nullable restore

        /// <summary>4-digit year, omitted if unparsable.</summary>
        [JsonProperty("y", NullValueHandling = NullValueHandling.Ignore)]
        public int? Year { get; set; }

        /// <summary>Song length in seconds.</summary>
        [JsonProperty("l")]
        public int Length { get; set; }

        /// <summary>Number of vocal parts (0 for instrumental).</summary>
        [JsonProperty("vp")]
        public int VocalParts { get; set; }

        /// <summary>
        /// Parts code: subset of present parts in V,G,D,K,B order (e.g. "VGD", "VGk", "VGDKB").
        /// Keys uses 'K' if ProKeys present, otherwise 'k'.
        /// </summary>
        [JsonProperty("p")]
        public string Parts { get; set; }

        /// <summary>
        /// Difficulty code: 5-character string in V,G,D,K,B order.
        /// Each character is '.', '?', or a base-36 tier digit (0-9, a-z).
        /// </summary>
        [JsonProperty("d")]
        public string Diff { get; set; }

        /// <summary>
        /// Genre index into the genres table, omitted if no genre.
        /// </summary>
        [JsonProperty("g", NullValueHandling = NullValueHandling.Ignore)]
        public int? Genre { get; set; }

        /// <summary>
        /// Creates a WebBrowserRecord from a SongEntry.
        /// </summary>
        /// <param name="song">The song entry to convert.</param>
        /// <param name="genres">The genre table for interning.</param>
        /// <returns>A compact record matching the export contract.</returns>
        public static WebBrowserRecord FromSong(SongEntry song, WebBrowserGenreTable genres)
        {
            // Strip rich text tags, then trim whitespace left behind by removed tags.
            string artist = RichTextUtils.StripRichTextTags(song.Artist).Trim();
            string title = RichTextUtils.StripRichTextTags(song.Name).Trim();
            string album = RichTextUtils.StripRichTextTags(song.Album).Trim();
            string genre = RichTextUtils.StripRichTextTags(song.Genre);

            // Parse year: try to extract a 4-digit year from UnmodifiedYear.
            int? year = ParseYear(song.UnmodifiedYear);

            // Length in seconds, truncated from double (same (int) cast as ExportCsv).
            int length = (int)song.SongLengthSeconds;

            // Vocal parts count.
            int vocalParts = song.VocalsCount;

            // Build parts code and difficulty code.
            string parts = BuildPartsCode(song);
            string diff = BuildDiffCode(song);

            // Intern genre.
            int? genreIndex = genres.Intern(genre);

            // Coerce empty album to null so NullValueHandling.Ignore omits the key from the JSON.
            return new WebBrowserRecord
            {
                Artist = artist,
                Title = title,
                Album = string.IsNullOrEmpty(album) ? null : album,
                Year = year,
                Length = length,
                VocalParts = vocalParts,
                Parts = parts,
                Diff = diff,
                Genre = genreIndex
            };
        }

        /// <summary>
        /// Creates a sorted list of records from an enumeration of songs.
        /// </summary>
        /// <param name="songs">The songs to convert.</param>
        /// <param name="genres">The genre table for interning.</param>
        /// <returns>A list sorted by (Artist, Title) using invariant ignore-case comparison.</returns>
        public static List<WebBrowserRecord> BuildAll(IEnumerable<SongEntry> songs, WebBrowserGenreTable genres)
        {
            var records = new List<WebBrowserRecord>();

            // First pass: convert all songs to records.
            foreach (var song in songs)
            {
                var record = FromSong(song, genres);
                records.Add(record);
            }

            // Second pass: sort by (Artist, Title) using culture-invariant ignore-case comparison.
            records.Sort((a, b) =>
            {
                int artistCompare = StringComparer.InvariantCultureIgnoreCase.Compare(a.Artist, b.Artist);
                if (artistCompare != 0)
                {
                    return artistCompare;
                }
                return StringComparer.InvariantCultureIgnoreCase.Compare(a.Title, b.Title);
            });

            return records;
        }

        /// <summary>
        /// Parses the year from UnmodifiedYear with a plain integer parse; null if
        /// unparsable (no extraction from decorated strings like "1984 (remaster)").
        /// </summary>
        private static int? ParseYear(string yearString)
        {
            if (int.TryParse(yearString, NumberStyles.Integer, CultureInfo.InvariantCulture, out int year))
            {
                return year;
            }

            return null;
        }

        /// <summary>
        /// Builds the parts code as the subset of present parts in V,G,D,K,B order
        /// (e.g. "VGD", "VGk", "VGDKB"). V requires lead Vocals, K appears only when
        /// 5-lane Keys is present (uppercase if ProKeys also present, else lowercase
        /// 'k'), and B covers 5/6-fret Bass only.
        /// </summary>
        private static string BuildPartsCode(SongEntry song)
        {
            string parts = "";

            // Vocals: lead vocals present (Harmony alone does not add V).
            if (song.HasInstrument(Instrument.Vocals)) parts += "V";

            // Guitar: 5-fret or 6-fret.
            if (song.HasInstrument(Instrument.FiveFretGuitar) ||
                song.HasInstrument(Instrument.SixFretGuitar)) parts += "G";

            // Drums: 4-lane, Pro, or 5-lane (Elite excluded — not a playable mode).
            if (song.HasInstrument(Instrument.FourLaneDrums) ||
                song.HasInstrument(Instrument.ProDrums) ||
                song.HasInstrument(Instrument.FiveLaneDrums)) parts += "D";

            // Keys: present when 5-lane Keys exists; 'K' if ProKeys also present, else 'k'.
            if (song.HasInstrument(Instrument.Keys))
            {
                parts += song.HasInstrument(Instrument.ProKeys) ? "K" : "k";
            }

            // Bass: 5-fret or 6-fret (rhythm/coop excluded).
            if (song.HasInstrument(Instrument.FiveFretBass) ||
                song.HasInstrument(Instrument.SixFretBass)) parts += "B";

            return parts;
        }

        /// <summary>
        /// Builds the difficulty code (V,G,D,K,B) with aggregation.
        /// </summary>
        private static string BuildDiffCode(SongEntry song)
        {
            // Helper: get intensity for an instrument, or -1 if not present.
            int GetIntensity(Instrument inst)
            {
                return song.HasInstrument(inst) ? song[inst].Intensity : -1;
            }

            // Helper: encode a slot from multiple sub-types.
            char EncodeSlot(params (bool exists, int intensity)[] subTypes)
            {
                return WebBrowserDifficulty.EncodeSlot(subTypes);
            }

            // Vocals: Vocals + Harmony (aggregate).
            char v = EncodeSlot(
                (song.HasInstrument(Instrument.Vocals), GetIntensity(Instrument.Vocals)),
                (song.HasInstrument(Instrument.Harmony), GetIntensity(Instrument.Harmony))
            );

            // Guitar: 5-fret + 6-fret guitar (aggregate).
            char g = EncodeSlot(
                (song.HasInstrument(Instrument.FiveFretGuitar), GetIntensity(Instrument.FiveFretGuitar)),
                (song.HasInstrument(Instrument.SixFretGuitar), GetIntensity(Instrument.SixFretGuitar))
            );

            // Drums: 4-lane + Pro + 5-lane (aggregate, Elite excluded).
            char d = EncodeSlot(
                (song.HasInstrument(Instrument.FourLaneDrums), GetIntensity(Instrument.FourLaneDrums)),
                (song.HasInstrument(Instrument.ProDrums), GetIntensity(Instrument.ProDrums)),
                (song.HasInstrument(Instrument.FiveLaneDrums), GetIntensity(Instrument.FiveLaneDrums))
            );

            // Keys: Keys + ProKeys (aggregate).
            char k = EncodeSlot(
                (song.HasInstrument(Instrument.Keys), GetIntensity(Instrument.Keys)),
                (song.HasInstrument(Instrument.ProKeys), GetIntensity(Instrument.ProKeys))
            );

            // Bass: 5-fret + 6-fret bass only (rhythm/coop excluded).
            char b = EncodeSlot(
                (song.HasInstrument(Instrument.FiveFretBass), GetIntensity(Instrument.FiveFretBass)),
                (song.HasInstrument(Instrument.SixFretBass), GetIntensity(Instrument.SixFretBass))
            );

            return new string(new[] { v, g, d, k, b });
        }
    }
}
