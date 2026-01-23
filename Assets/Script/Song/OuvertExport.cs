using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using YARG.Core;
using YARG.Core.Song;
using YARG.Core.Utility;

namespace YARG.Song
{
    public static class OuvertExport
    {
        private class OuvertSongData
        {
            [JsonProperty("Name")]
            public string songName;

            [JsonProperty("Artist")]
            public string artistName;

            [JsonProperty("Album")]
            public string album;

            [JsonProperty("Playlist")]
            public string playlist;

            [JsonProperty("Genre")]
            public string genre;

            [JsonProperty("Charter")]
            public string charter;

            [JsonProperty("Year")]
            public string year;

            // public bool lyrics;
            [JsonProperty("songlength")]
            public ulong songLength;

            [JsonProperty("chartsAvailable")]
            public ulong chartsAvailable;
        }

        private const int DIFFS_PER_INSTRUMENT = 6;

        public static void Export(string path)
        {
            var songs = new List<OuvertSongData>();

            // Convert SongInfo to OuvertSongData
            foreach (var song in SongContainer.Songs)
            {
                var data = new OuvertSongData
                {
                    songName = RichTextUtils.StripRichTextTags(song.Name),
                    artistName = RichTextUtils.StripRichTextTags(song.Artist),
                    album = RichTextUtils.StripRichTextTags(song.Album),
                    genre = RichTextUtils.StripRichTextTags(song.Genre),
                    charter = RichTextUtils.StripRichTextTags(song.Charter),
                    year = RichTextUtils.StripRichTextTags(song.UnmodifiedYear),
                    songLength = (ulong) song.SongLengthMilliseconds,
                    chartsAvailable = GetChartsAvailable(song)
                };

                if (RichTextUtils.StripRichTextTags(song.Playlist) != "Unknown Playlist")
                {
                    data.playlist = RichTextUtils.StripRichTextTags(song.Playlist);
                }

                songs.Add(data);
            }

            // Create file
            var json = JsonConvert.SerializeObject(songs, Formatting.Indented, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            });
            File.WriteAllText(path, json);
        }

        // Returns a bitmask where each bit represents an available instrument/difficulty combination.
        // Bit index = (instrumentId * 6) + difficulty
        // To check if a chart is available: (chartsAvailable & (1 << bitIndex)) != 0
        private static ulong GetChartsAvailable(SongEntry song)
        {
            ulong chartsAvailable = 0;
            foreach (Instrument instrument in System.Enum.GetValues(typeof(Instrument)))
            {
                chartsAvailable |= GetMask(song, instrument);
            }

            return chartsAvailable;
        }

        /// Calculates the bitmask for a specific instrument's available difficulties.
        /// The bit index is calculated as: (instrumentId * DIFFS_PER_INSTRUMENT) + (int)difficulty.
        private static ulong GetMask(SongEntry song, Instrument inst)
        {
            int id = GetInstrumentId(inst);
            if (id == -1)
            {
                return 0;
            }

            return (ulong) song[inst].Difficulties << (id * DIFFS_PER_INSTRUMENT);
        }

        // Re-map instrument ids, since a ulong can only fit 10 instruments (64 bits / 6 difficulties = 10 instruments)
        private static int GetInstrumentId(Instrument instrument)
        {
            return instrument switch
            {
                Instrument.FiveFretGuitar => 0,
                Instrument.FiveFretBass   => 1,
                Instrument.FiveFretRhythm => 2,
                Instrument.FourLaneDrums  => 3,
                Instrument.FiveLaneDrums  => 4,
                Instrument.ProDrums       => 5,
                Instrument.Keys           => 6,
                Instrument.ProKeys        => 7,
                Instrument.Vocals         => 8,
                Instrument.Harmony        => 9,
                _                         => -1
            };
        }
    }
}