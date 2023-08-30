using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using YARG.Helpers;
using YARG.Song;

namespace YARG.Integration
{
    public class CurrentSongController : MonoBehaviour
    {
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
            // Create blank files if we exit playmode or the SongMetadata is not present
            if (state.CurrentScene != SceneIndex.Gameplay || state.SongMetadata is null)
            {
                CreateBlankFiles();
                return;
            }

            var song = state.SongMetadata;

            // Get the input
            string str =
                $"{song.Name}\n" +
                $"{song.Artist}\n" +
                $"{song.Album}\n" +
                $"{song.Genre}\n" +
                $"{song.Year}\n" +
                $"{SongSources.SourceToGameName(song.Source)}\n" +
                $"{song.Charter}";

            // Strip tags
            str = RichTextUtils.StripRichTextTags(str);

            // Convert to JSON
            string json = JsonConvert.SerializeObject(song);

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