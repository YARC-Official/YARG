using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using YARG.Core;
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
            Csv
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
                    string playlist = RichTextUtils.StripRichTextTags(song.Playlist);

                    if (playlist == "Unknown Playlist")
                    {
                        output.WriteLine($"{artist} - {name}");
                    }
                    else
                    {
                        output.WriteLine($"{artist} - {name} from {playlist}");
                    }
                }

                output.WriteLine("");
            }

            output.Flush();
        }

        private static void ExportJson(string path)
        {
            OuvertExport.Export(path);
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
    }
}
