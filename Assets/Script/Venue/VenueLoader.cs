using System.IO;
using System.Collections.Generic;
using UnityEngine;
using YARG.Helpers;
using YARG.Settings;
using YARG.Core.Song;
using YARG.Core.Venue;

namespace YARG.Venue
{
    public enum VenueSource
    {
        Global,
        Song,
    }

    public readonly struct VenueInfo
    {
        public readonly VenueSource Source;
        public readonly BackgroundType Type;
        public readonly Stream Stream;

        public VenueInfo(VenueSource source, BackgroundType type, Stream stream)
        {
            Source = source;
            Type = type;
            Stream = stream;
        }
    }

    public static class VenueLoader
    {
        private static string _venueFolder = null;
        public static string VenueFolder => _venueFolder ??= Path.Combine(PathHelper.PersistentDataPath, "venue");

        static VenueLoader()
        {
            if (!Directory.Exists(VenueFolder))
            {
                Directory.CreateDirectory(VenueFolder);
            }
        }

        public static VenueInfo? GetVenue(SongEntry song)
        {
            const VenueSource songSource = VenueSource.Song;

            // If local backgrounds are disabled, skip right to global
            if (SettingsManager.Settings.DisablePerSongBackgrounds.Value)
            {
                return GetVenuePathFromGlobal();
            }

            var result = song.LoadBackground(
                BackgroundType.Image |
                BackgroundType.Video |
                BackgroundType.Yarground);

            if (result != null)
            {
                return new VenueInfo(songSource, result.Type, result.Stream);
            }
            return GetVenuePathFromGlobal();
        }

        private static VenueInfo? GetVenuePathFromGlobal()
        {
            const VenueSource globalSource = VenueSource.Global;

            // If global backgrounds are disabled, do not load anything here
            if (SettingsManager.Settings.DisableGlobalBackgrounds.Value)
            {
                return null;
            }

            string[] validExtensions =
            {
                "*.yarground", "*.mp4", "*.mov", "*.webm", "*.png", "*.jpg", "*.jpeg"
            };

            List<string> filePaths = new();
            foreach (string ext in validExtensions)
            {
                foreach (var file in Directory.EnumerateFiles(VenueFolder, ext, PathHelper.SafeSearchOptions))
                {
                    filePaths.Add(file);
                }
            }

            if (filePaths.Count <= 0)
            {
                return null;
            }

            var path = filePaths[Random.Range(0, filePaths.Count)];

            var extension = Path.GetExtension(path);
            var stream = File.OpenRead(path);

            return extension switch
            {
                ".yarground"                => new(globalSource, BackgroundType.Yarground, stream),
                ".mp4" or ".mov" or ".webm" => new(globalSource, BackgroundType.Video, stream),
                ".png" or ".jpg" or ".jpeg" => new(globalSource, BackgroundType.Image, stream),
                _                           => null,
            };
        }
    }
}