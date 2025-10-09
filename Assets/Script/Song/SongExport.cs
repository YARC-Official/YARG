using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
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
            output.WriteLine("Name,Artist,Album,Genre,Year,Length");
            foreach (var song in SongContainer.Songs)
            {
                string name = Escape(RichTextUtils.StripRichTextTags(song.Name));
                string artist = Escape(RichTextUtils.StripRichTextTags(song.Artist));
                string album = Escape(RichTextUtils.StripRichTextTags(song.Album));
                string genre = Escape(RichTextUtils.StripRichTextTags(song.Genre));
                string year = Escape(RichTextUtils.StripRichTextTags(song.UnmodifiedYear));

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
            var songCategories = SongContainer.GetSortedCategory(SortAttribute.Artist);
            int totalSongs = songCategories.Sum(category => category.Songs.Length);
            const int columnCount = 5;
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1, Unit.Centimetre);
                    page.DefaultTextStyle(textStyle => textStyle
                        .FontSize(7)
                    );
                    page.Header()
                        .ShowOnce()
                        .AlignCenter()
                        .PaddingBottom(12)
                        .Text($"YARG Songbook - {totalSongs} Songs")
                        .Bold()
                        .FontSize(8);
                    page.Content().MultiColumn(multiColumn =>
                    {
                        multiColumn.Columns(columnCount);
                        multiColumn.Spacing(0.5f);
                        multiColumn.BalanceHeight();
                        multiColumn
                            .Spacer()
                            .AlignCenter()
                            .LineVertical(0.5f)
                            .LineColor(Colors.Grey.Medium);
                        multiColumn
                            .Content()
                            .Column(column =>
                            {
                                column.Spacing(0);
                                foreach ((string artist, var songs) in songCategories)
                                {
                                    column
                                        .Item()
                                        .Column(sectionColumn =>
                                        {
                                            //Artist
                                            sectionColumn.Item()
                                                .Background("#F5F5F5")
                                                .Padding(4)
                                                .BorderBottom(Colors.Grey.Darken4)
                                                .Text(RichTextUtils.StripRichTextTags(artist))
                                                .Bold()
                                                .ClampLines(1);

                                            foreach (var song in songs)
                                            {
                                                sectionColumn
                                                    .Item()
                                                    .PaddingLeft(8)
                                                    .PaddingRight(4)
                                                    .Text(RichTextUtils.StripRichTextTags(song.Name))
                                                    .ClampLines(1);
                                            }
                                        });
                                }
                            });
                    });
                    page.Footer()
                        .AlignCenter()
                        .PaddingTop(4)
                        .Text(text =>
                        {
                            text.CurrentPageNumber();
                            text.Span(" / ");
                            text.TotalPages();
                        });
                });
            });
            document.GeneratePdf(path);
        }
    }
}