using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using YARG.Core.Song;
using YARG.Core.Utility;
using YARG.Helpers;
using YARG.Song;

namespace YARG.Integration
{
    public class CurrentSongController : MonoBehaviour
    {
        // TODO: We may wanna explicitly specify the json output by putting it in a new class
        private class SortStringConverter : JsonConverter<SortString>
        {
            public static readonly SortStringConverter Default = new();

            public override void WriteJson(JsonWriter writer, SortString value, JsonSerializer serializer)
            {
                writer.WriteValue(value.Original);
            }

            public override SortString ReadJson(JsonReader reader, Type objectType, SortString existingValue,
                bool hasExistingValue, JsonSerializer serializer)
            {
                // We aren't reading from currentSong.json
                throw new InvalidOperationException();
            }

            public override bool CanRead => false;
        }

        // Creates .TXT file with current song information
        public static string TextFilePath => Path.Combine(PathHelper.PersistentDataPath, "currentSong.txt");

        // Creates .JSON file with current song information
        public static string JsonFilePath => Path.Combine(PathHelper.PersistentDataPath, "currentSong.json");

        private bool _fileShouldBeBlank;

        private void Start()
        {
            CreateBlankFiles();

            GameStateFetcher.GameStateChange += OnGameStateChange;
        }

        private void OnDestroy()
        {
            CreateBlankFiles();

            GameStateFetcher.GameStateChange -= OnGameStateChange;
        }

        private void CreateBlankFiles()
        {
            if (_fileShouldBeBlank) return;

            // Open the text file for appending
            using var writer = new StreamWriter(TextFilePath, false);
            using var jsonWriter = new StreamWriter(JsonFilePath, false);

            // Make the file blank (Avoid errors in OBS)
            writer.Write("");
            jsonWriter.Write("");

            _fileShouldBeBlank = true;
        }

        private void OnGameStateChange(GameStateFetcher.State state)
        {
            // Create blank files if we exit playmode or the SongEntry is not present
            if (state.CurrentScene != SceneIndex.Gameplay || state.SongEntry is null)
            {
                CreateBlankFiles();
                return;
            }

            var song = state.SongEntry;

            // Get the input
            string str =
                $"{song.Name}\n" +
                $"{song.Artist}\n" +
                $"{song.Album}\n" +
                $"{song.Genre}\n" +
                $"{song.ParsedYear}\n" +
                $"{SongSources.SourceToGameName(song.Source)}\n" +
                $"{song.Charter}";

            // Strip tags
            str = RichTextUtils.StripRichTextTags(str);

            // Convert to JSON
            string json = JsonConvert.SerializeObject(song, SortStringConverter.Default);

            // Open the text file for appending
            using var writer = new StreamWriter(TextFilePath, false);
            using var jsonWriter = new StreamWriter(JsonFilePath, false);

            // Write text to the file
            writer.Write(str);
            jsonWriter.Write(json);

            _fileShouldBeBlank = false;
        }
    }
}