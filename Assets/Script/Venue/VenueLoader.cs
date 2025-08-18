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
        private static readonly string _venueFolder = Path.Combine(PathHelper.PersistentDataPath, "venue");
        public static string VenueFolder
        {
            get
            {
                if (!Directory.Exists(_venueFolder))
                {
                    Directory.CreateDirectory(_venueFolder);
                }
                return _venueFolder;
            }
        }

#nullable enable
        public static BackgroundResult? GetVenue(SongEntry song, out VenueSource source)
        {
            BackgroundResult? result = null;
#nullable disable
            source = VenueSource.Song;
            if (!SettingsManager.Settings.DisablePerSongBackgrounds.Value)
            {
                result = song.LoadBackground();
            }

            if (!SettingsManager.Settings.DisableGlobalBackgrounds.Value && result == null)
            {
                source = VenueSource.Global;
                result = GetVenuePathFromGlobal();
            }
            return result;
        }

#nullable enable
        private static BackgroundResult? GetVenuePathFromGlobal()
#nullable disable
        {
            string[] validExtensions =
            {
                "*.yarground", "*.mp4", "*.mov", "*.webm", "*.png", "*.jpg", "*.jpeg"
            };

            string venueFolder = VenueFolder;
            var filePaths = new List<string>();
            foreach (var ext in validExtensions)
            {
                filePaths.AddRange(Directory.EnumerateFiles(venueFolder, ext, PathHelper.SafeSearchOptions));
            }

            while (filePaths.Count > 0)
            {
                int index = Random.Range(0, filePaths.Count);
                var file = filePaths[index];
                switch (Path.GetExtension(file))
                {
                    case ".png":
                    case ".jpg":
                    case ".jpeg":
                        var image = YARGImage.Load(file);
                        if (image != null)
                        {
                            return new BackgroundResult(image);
                        }
                        break;
                    case ".mp4":
                    case ".mov":
                    case ".webm":
                        return new BackgroundResult(BackgroundType.Video, File.OpenRead(file));
                    case ".yarground":
                        return new BackgroundResult(BackgroundType.Yarground, File.OpenRead(file));
                    default:
                        filePaths.RemoveAt(index);
                        break;
                }
            }
            return null;
        }
    }
}