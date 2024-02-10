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

            if (song.IniData != null)
            {
                var (type, stream) = song.IniData.GetBackgroundStream(
                    BackgroundType.Yarground |
                    BackgroundType.Video |
                    BackgroundType.Image);

                if (stream != null)
                    return new VenueInfo(songSource, type, stream);
            }
            else if (song.RBData is SongMetadata.RBUnpackedCONMetadata)
            {
                // Try a local yarground first
                string directory = song.Directory;
                string backgroundPath = Path.Combine(directory, "bg.yarground");
                if (File.Exists(backgroundPath))
                {
                    var stream = File.OpenRead(backgroundPath);
                    return new(songSource, BackgroundType.Yarground, stream);
                }

                // Then, a local picture or video

                string[] fileNames =
                {
                    "bg", "background", "video"
                };

                string[] videoExtensions =
                {
                    ".mp4", ".mov", ".webm",
                };

                foreach (var name in fileNames)
                {
                    var fileBase = Path.Combine(directory, name);
                    foreach (var ext in videoExtensions)
                    {
                        backgroundPath = fileBase + ext;
                        if (File.Exists(backgroundPath))
                        {
                            var stream = File.OpenRead(backgroundPath);
                            return new(songSource, BackgroundType.Video, stream);
                        }
                    }
                }

                string[] imageExtensions =
                {
                    ".png", ".jpg", ".jpeg",
                };

                foreach (var name in fileNames)
                {
                    var fileBase = Path.Combine(directory, name);
                    foreach (var ext in imageExtensions)
                    {
                        backgroundPath = fileBase + ext;

                        if (File.Exists(backgroundPath))
                        {
                            var stream = File.OpenRead(backgroundPath);
                            return new(songSource, BackgroundType.Image, stream);
                        }
                    }
                }
            }
            

            // If all of this fails, we can load a global venue
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