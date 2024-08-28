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
        private static readonly string _venueFolder;
        public static DirectoryInfo VenueFolder
        {
            get
            {
                if (!Directory.Exists(_venueFolder))
                {
                    Directory.CreateDirectory(_venueFolder);
                }
                return new DirectoryInfo(_venueFolder);
            }
        }

        static VenueLoader()
        {
            _venueFolder = Path.Combine(PathHelper.PersistentDataPath, "venue");
        }

#nullable enable
        public static BackgroundResult? GetVenue(SongEntry song, out VenueSource source)
        {
            BackgroundResult? result = null;
#nullable disable
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

#nullable enable
        private static BackgroundResult? GetVenuePathFromGlobal()
#nullable disable
        {
            string[] validExtensions =
            {
                "*.yarground", "*.mp4", "*.mov", "*.webm", "*.png", "*.jpg", "*.jpeg"
            };

            var dirInfo = VenueFolder;
            var filePaths = new List<FileInfo>();
            foreach (var ext in validExtensions)
            {
                filePaths.AddRange(dirInfo.EnumerateFiles(ext, PathHelper.SafeSearchOptions));
            }

            while (filePaths.Count > 0)
            {
                int index = Random.Range(0, filePaths.Count);
                var info = filePaths[index];
                switch (info.Extension)
                {
                    case ".png":
                    case ".jpg":
                    case ".jpeg":
                        var image = YARGImage.LoadFile(info);
                        if (image != null)
                        {
                            return new BackgroundResult(image);
                        }
                        break;
                    case ".mp4":
                    case ".mov":
                    case ".webm":
                        return new BackgroundResult(BackgroundType.Video, File.OpenRead(info.FullName));
                    case ".yarground":
                        return new BackgroundResult(BackgroundType.Yarground, File.OpenRead(info.FullName));
                    default:
                        filePaths.RemoveAt(index);
                        break;
                }
            }
            return null;
        }
    }
}