﻿using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using UnityEngine;
using YARG.Core.Song;
using YARG.Helpers;
using YARG.PlayMode;
using YARG.Song;

namespace YARG
{
    public class TwitchController : MonoSingleton<TwitchController>
    {
        private static Regex TagRegex = new(@"<[^>]*>", RegexOptions.Compiled);

        // Creates .TXT file with current song information
        public string TextFilePath => Path.Combine(PathHelper.PersistentDataPath, "currentSong.txt");

        // Creates .JSON file with current song information
        public string JsonFilePath => Path.Combine(PathHelper.PersistentDataPath, "currentSong.json");

        private void Start()
        {
            // While YARG should blank the file on exit, you never know if a crash or something prevented that.
            BlankSongFile();

            // Listen to the changing of songs
            Play.OnSongStart += OnSongStart;
            Play.OnSongEnd += OnSongEnd;

            // TODO: FIX
            // Listen to instrument selection - NYI, let's confirm the rest works
            // DifficultySelect.OnInstrumentSelection += OnInstrumentSelection;

            // Listen to pausing - NYI, let's confirm the rest works
            Play.OnPauseToggle += OnPauseToggle;
        }

        private void BlankSongFile()
        {
            // Open the text file for appending
            using var writer = new StreamWriter(TextFilePath, false);
            using var jsonWriter = new StreamWriter(JsonFilePath, false);

            // Make the file blank (Avoid errors in OBS)
            writer.Write("");
            jsonWriter.Write("");
        }

        private void OnApplicationQuit()
        {
            BlankSongFile();
        }

        void OnSongStart(SongMetadata song)
        {
            // Open the text file for appending
            using var writer = new StreamWriter(TextFilePath, false);
            using var jsonWriter = new StreamWriter(JsonFilePath, false);

            // Get the input
            string str = $"{song.Name}\n{song.Artist}\n{song.Album}\n{song.Genre}\n" +
                $"{song.Year}\n{SongSources.SourceToGameName(song.Source)}\n{song.Charter}";

            // Strip tags
            if (TagRegex.IsMatch(str))
            {
                str = TagRegex.Replace(str, string.Empty);
            }

            // Convert to JSON
            string json = JsonConvert.SerializeObject(song);

            // Write text to the file
            writer.Write(str);
            jsonWriter.Write(json);
        }

        void OnSongEnd(SongMetadata song)
        {
            BlankSongFile();
        }

        // private void OnInstrumentSelection(PlayerManager.Player playerInfo)
        // {
        //     // Selecting Instrument
        // }

        private void OnPauseToggle(bool pause)
        {
            // Game Paused
        }
    }
}