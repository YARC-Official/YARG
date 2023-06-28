using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using YARG.Song;

namespace YARG.Serialization
{
    public static class OuvertExport
    {
        private class SongData
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
            public int songLength;
        }

        public static void ExportOuvertSongsTo(string path)
        {
            var songs = new List<SongData>();

            // Convert SongInfo to SongData
            foreach (var song in SongContainer.Songs)
            {
                songs.Add(new SongData
                {
                    songName = song.Name,
                    artistName = song.Artist,
                    album = song.Album,
                    genre = song.Genre,
                    charter = song.Charter,
                    year = song.Year,
                    songLength = (int) (song.SongLength * 1000f)
                });
            }

            // Create file
            var json = JsonConvert.SerializeObject(songs);
            File.WriteAllText(path, json);
        }
    }
}