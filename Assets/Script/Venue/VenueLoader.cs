using System.IO;
using System.Collections.Generic;
using UnityEngine;
using YARG.Helpers;
using YARG.Settings;
using YARG.Core.Song;
using YARG.Core.Venue;
using YARG.Core.IO;

namespace YARG.Venue
{
    public enum VenueSource
    {
        Global,
        Song,
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

        public static BackgroundResult? GetVenue(SongEntry song, out VenueSource source)
        {
            BackgroundResult? result = null;
            source = VenueSource.Song;
            if (!SettingsManager.Settings.DisablePerSongBackgrounds.Value)
            {
                result = song.LoadBackground(
                    BackgroundType.Image |
                    BackgroundType.Video |
                    BackgroundType.Yarground
                );
            }

            if (!SettingsManager.Settings.DisableGlobalBackgrounds.Value && result == null)
            {
                source = VenueSource.Global;
                result = GetVenuePathFromGlobal();
            }
            return result;
        }

        private static BackgroundResult? GetVenuePathFromGlobal()
        {
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

            while (filePaths.Count > 0)
            {
                int index = Random.Range(0, filePaths.Count);
                var path = filePaths[index];
                var extension = Path.GetExtension(path);

                if (extension is ".png" or ".jpg" or ".jpeg")
                {
                    var image = YARGImage.Load(path);
                    if (image != null)
                    {
                        return new BackgroundResult(image);
                    }
                }
                else
                {
                    var stream = File.OpenRead(path);
                    if (stream != null)
                    {
                        if (extension == ".yarground")
                        {
                            return new BackgroundResult(BackgroundType.Yarground, stream);
                        }

                        if (extension is ".mp4" or ".mov" or ".webm")
                        {
                            return new BackgroundResult(BackgroundType.Video, stream);
                        }
                        stream.Dispose();
                    }
                }
                filePaths.RemoveAt(index);
            }
            return null;
        }
    }
}