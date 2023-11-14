using System.IO;
using System.Collections.Generic;
using UnityEngine;
using YARG.Helpers;
using YARG.Settings;
using YARG.Core.Song;

namespace YARG.Venue
{
    public enum VenueSource
    {
        Global,
        Song,
    }

    public enum VenueType
    {
        Yarground,
        Video,
        Image
    }

    public readonly struct VenueInfo
    {
        public readonly VenueSource Source;
        public readonly VenueType Type;
        public readonly string Path;

        public VenueInfo(VenueSource source, VenueType type, string path)
        {
            Source = source;
            Type = type;
            Path = path;
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

        public static VenueInfo? GetVenue(SongMetadata song)
        {
            const VenueSource songSource = VenueSource.Song;

            // If local backgrounds are disabled, skip right to global
            if (SettingsManager.Settings.DisablePerSongBackgrounds.Data)
            {
                return GetVenuePathFromGlobal();
            }

            // Try a local yarground first
            string directory = song.Directory;
            string backgroundPath = Path.Combine(directory, "bg.yarground");
            if (File.Exists(backgroundPath))
            {
                return new(songSource, VenueType.Yarground, backgroundPath);
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
                    var path = fileBase + ext;
                    if (File.Exists(path))
                    {
                        return new(songSource, VenueType.Video, path);
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
                    var path = fileBase + ext;

                    if (File.Exists(path))
                    {
                        return new(songSource, VenueType.Image, path);
                    }
                }
            }

            // If all of this fails, we can load a global venue
            return GetVenuePathFromGlobal();
        }

        private static VenueInfo? GetVenuePathFromGlobal()
        {
            const VenueSource globalSource = VenueSource.Global;
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

            return extension switch
            {
                ".yarground"                => new(globalSource, VenueType.Yarground, path),
                ".mp4" or ".mov" or ".webm" => new(globalSource, VenueType.Video, path),
                ".png" or ".jpg" or ".jpeg" => new(globalSource, VenueType.Image, path),
                _                           => null,
            };
        }
    }
}