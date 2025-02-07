using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
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
            // TODO: Allow customizing sorting, as well as which metadata is written and in what order

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
                    songLength = (ulong)song.SongLengthMilliseconds
                });
            }

            // Create file
            var json = JsonConvert.SerializeObject(songs, Formatting.Indented);
            File.WriteAllText(path, json);
        }
    }
}