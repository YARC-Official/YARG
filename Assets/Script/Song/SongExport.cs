using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using YARG.Core.Song;
using YARG.Core.Utility;

namespace YARG.Song
{
    public static class SongExport
    {
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

        public static void ExportText(string path)
        {
            // Ensure directory exists
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

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

        public static void ExportOuvert(string path)
        {
            var songs = new List<OuvertSongData>();

            // Convert SongInfo to OuvertSongData
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

            // Ensure directory exists
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Create file
            var json = JsonConvert.SerializeObject(songs, Formatting.Indented);
            File.WriteAllText(path, json);
        }

        public static void ExportPdf(string path)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            // Ensure directory exists
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var artistCategory = SongContainer.GetSortedCategory(SortAttribute.Artist);
            int totalArtists = artistCategory.Length;
            int artistsPerColumn = (int) Math.Ceiling(totalArtists / 3.0); // Round up to distribute evenly

            // Split artists into 3 columns using LINQ
            var columns = artistCategory
                .Select((artist, index) => new
                {
                    Artist = artist,
                    Column = Math.Min(index / artistsPerColumn, 2)
                })
                .GroupBy(x => x.Column)
                .Select(g => g.Select(x => x.Artist).ToList())
                .ToList();

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(7));
                    page.Content().Row(row =>
                    {
                        row.Spacing(5);

                        foreach (var columnArtists in columns)
                        {
                            row.RelativeItem().Column(column =>
                            {
                                column.Spacing(3);

                                foreach (var (category, songs) in columnArtists)
                                {
                                    // Artist name in bold
                                    column.Item().Text(RichTextUtils.StripRichTextTags(category))
                                        .Bold()
                                        .FontSize(8);

                                    // Songs list
                                    foreach (var song in songs)
                                    {
                                        string name = RichTextUtils.StripRichTextTags(song.Name);
                                        column.Item().PaddingLeft(8).Text(name);
                                    }

                                    // Add spacing between artist groups
                                    column.Item().PaddingBottom(2);
                                }
                            });
                        }
                    });
                });
            });

            document.GeneratePdf(path);
        }
    }
}