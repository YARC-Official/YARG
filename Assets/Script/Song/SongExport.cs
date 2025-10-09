using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Primitives;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using YARG.Core.Song;
using YARG.Core.Utility;
using YARG.Helpers;

namespace YARG.Song
{
    public static class SongExport
    {
        public enum ExportFormat
        {
            Json,
            Text,
            Pdf,
            Csv
        }

        private class OuvertSongData
        {
            [JsonProperty("Name")]
            public string songName;

            [JsonProperty("Artist")]
            public string artistName;

            [JsonProperty("Album")]
            public string album;

            [JsonProperty("Genre")]
            public string genre;

            [JsonProperty("Charter")]
            public string charter;

            [JsonProperty("Year")]
            public string year;

            // public bool lyrics;
            [JsonProperty("songlength")]
            public ulong songLength;
        }

        public static void Export(ExportFormat format)
        {
            switch (format)
            {
                case ExportFormat.Json:
                    FileExplorerHelper.OpenSaveFile(null, "songs", "json", ExportJson);
                    break;
                case ExportFormat.Text:
                    FileExplorerHelper.OpenSaveFile(null, "songs", "txt", ExportText);
                    break;
                case ExportFormat.Pdf:
                    FileExplorerHelper.OpenSaveFile(null, "songs", "pdf", ExportPdf);
                    break;
                case ExportFormat.Csv:
                    FileExplorerHelper.OpenSaveFile(null, "songs", "csv", ExportCsv);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(format), format, null);
            }
        }

        private static void ExportText(string path)
        {
            using var output = new StreamWriter(path);
            foreach (var (category, songs) in SongContainer.GetSortedCategory(SortAttribute.Artist))
            {
                output.WriteLine(category);
                output.WriteLine("--------------------");
                foreach (var song in songs)
                {
                    string artist = RichTextUtils.StripRichTextTags(song.Artist);
                    string name = RichTextUtils.StripRichTextTags(song.Name);
                    output.WriteLine($"{artist} - {name}");
                }

                output.WriteLine("");
            }

            output.Flush();
        }

        private static void ExportJson(string path)
        {
            var songs = new List<OuvertSongData>();
            foreach (var song in SongContainer.Songs)
            {
                songs.Add(new OuvertSongData
                {
                    songName = RichTextUtils.StripRichTextTags(song.Name),
                    artistName = RichTextUtils.StripRichTextTags(song.Artist),
                    album = RichTextUtils.StripRichTextTags(song.Album),
                    genre = RichTextUtils.StripRichTextTags(song.Genre),
                    charter = RichTextUtils.StripRichTextTags(song.Charter),
                    year = RichTextUtils.StripRichTextTags(song.UnmodifiedYear),
                    songLength = (ulong) song.SongLengthMilliseconds
                });
            }

            var json = JsonConvert.SerializeObject(songs, Formatting.Indented);
            File.WriteAllText(path, json);
        }

        private static void ExportCsv(string path)
        {
            using var output = new StreamWriter(path);

            // Write header row
            output.WriteLine("Name,Artist,Album,Genre,Year,Length");

            foreach (var song in SongContainer.Songs)
            {
                string name = Escape(RichTextUtils.StripRichTextTags(song.Name));
                string artist = Escape(RichTextUtils.StripRichTextTags(song.Artist));
                string album = Escape(RichTextUtils.StripRichTextTags(song.Album));
                string genre = Escape(RichTextUtils.StripRichTextTags(song.Genre));
                string year = Escape(RichTextUtils.StripRichTextTags(song.UnmodifiedYear));

                // Format song length as minutes:seconds
                int totalSeconds = (int) song.SongLengthSeconds;
                int minutes = totalSeconds / 60;
                int seconds = totalSeconds % 60;
                string songLength = $"{minutes}:{seconds:D2}";
                output.WriteLine($"{name},{artist},{album},{genre},{year},{songLength}");
            }

            output.Flush();

            string Escape(string field)
            {
                const string quote = "\"";
                const string escapedQuote = "\"\"";

                if (string.IsNullOrEmpty(field))
                {
                    return "";
                }

                bool needsEscaping = field.Contains(',')
                    || field.Contains('"')
                    || field.Contains('\n')
                    || field.Contains('\r');

                if (needsEscaping)
                {
                    string escaped = field.Replace(quote, escapedQuote);
                    return $"{quote}{escaped}{quote}";
                }

                return field;
            }
        }

        private static void ExportPdf(string path)
        {
            QuestPDF.Settings.License = LicenseType.Community;
            SongCategory[] songCategories = SongContainer.GetSortedCategory(SortAttribute.Artist);
            int totalSongs = songCategories.Sum(category => category.Songs.Length);
            int totalItems = songCategories.Sum(category => 1 + category.Songs.Length);
            int maxColumnLength = (int) Math.Ceiling(totalItems / 3.0);
            var columns = new List<List<SongCategory>>
            {
                new(),
                new(),
                new()
            };
            int columnIndex = 0;
            int currentLength = 0;
            foreach (var category in songCategories)
            {
                int categoryLength = 1 + category.Songs.Length;
                bool willExceedMaxColumnSize = currentLength + categoryLength > maxColumnLength;
                bool isLastColumn = columnIndex == 2;
                bool columnHasItems = currentLength > 0;

                // Move to next column if we will exceed max column size when adding this artist
                // But not if we are on the last column
                // And not if the current column is empty
                bool shouldMoveToNextColumn = willExceedMaxColumnSize && !isLastColumn && columnHasItems;
                if (shouldMoveToNextColumn)
                {
                    columnIndex++;
                    currentLength = 0;
                }

                columns[columnIndex].Add(category);
                currentLength += categoryLength;
            }

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(7));
                    page.Header()
                        .ShowOnce()
                        .AlignCenter()
                        .PaddingBottom(12)
                        .Text($"YARG Songbook - {totalSongs} Songs")
                        .Bold()
                        .FontSize(8);
                    page.Content().Row(row =>
                    {
                        row.Spacing(10);
                        foreach (var columnData in columns)
                        {
                            row.RelativeItem().Column(column =>
                            {
                                column.Spacing(3);
                                foreach ((string artist, var songs) in columnData)
                                {
                                    column
                                        .Item()
                                        .Text(RichTextUtils.StripRichTextTags(artist))
                                        .Bold();
                                    foreach (var song in songs)
                                    {
                                        column
                                            .Item()
                                            .PaddingLeft(8)
                                            .Text(RichTextUtils.StripRichTextTags(song.Name));
                                    }
                                    column.Item().PaddingBottom(2);
                                }
                            });
                        }
                    });
                    page.Footer()
                        .AlignCenter()
                        .Text(x => { x.CurrentPageNumber(); });
                });
            });
            document.GeneratePdf(path);
        }
    }
}