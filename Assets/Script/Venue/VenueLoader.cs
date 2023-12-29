using System.IO;
using System.Collections.Generic;
using UnityEngine;
using YARG.Helpers;
using YARG.Settings;
using YARG.Core.Song;
using YARG.Core.Venue;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using System.Runtime.InteropServices;
using YARG.Helpers.Extensions;
using YARG.Song;
using UnityEngine.UI;
using YARG.Gameplay;
using YARG.Gameplay.HUD;

namespace YARG.Venue
{
    public enum VenueSource
    {
        Global,
        Song,
    }

    public enum VenueMode
    {
        Default,
        GlobalVenuesOnly,
        AlbumAsBackground,
        OverrideToAlbumAsBackground
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

        public static VenueInfo? GetVenue(SongMetadata song)
        {
            const VenueSource songSource = VenueSource.Song;

            // If local backgrounds are disabled, skip right to global
            if (SettingsManager.Settings.BackgroundMode.Value == VenueMode.GlobalVenuesOnly)
            {
                return GetVenuePathFromGlobal();
            }

             // If album bg override is enabled, jump to that
            if (SettingsManager.Settings.BackgroundMode.Value == VenueMode.OverrideToAlbumAsBackground)
            {
                return CreateBgFromAlbum(song);
            }
                

            if (song.IniData != null)
            {
                var stream = song.IniData.GetBackgroundStream(
                    BackgroundType.Yarground |
                    BackgroundType.Video |
                    BackgroundType.Image |
                    BackgroundType.Album);

                if (stream.Item2 != null)
                    return new VenueInfo(songSource, stream.Item1, stream.Item2);
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
            // If all fails, create background from album, if enabled
            if (SettingsManager.Settings.BackgroundMode.Value == VenueMode.AlbumAsBackground)
            {
                return CreateBgFromAlbum(song);
            }
            else // If disabled, jump to global venues
            {
                return GetVenuePathFromGlobal();
            }
        }
        public static VenueInfo? CreateBgFromAlbum(SongMetadata song)
        {
            const VenueSource songSource = VenueSource.Song;
            string adirectory = song.Directory;
            
            string[] albumNames =
            {
                "album"
            };

            string[] albumExtensions = 
            {
                ".png", ".jpg", ".jpeg"
            };

            foreach (var aname in albumNames)
            {
                var aFile = Path.Combine(adirectory, aname);
                foreach (var ext in albumExtensions)
                {
                    string albumPath = aFile + ext;

                    if (File.Exists(albumPath))
                    {
                        var stream = File.OpenRead(albumPath);
                        return new(songSource, BackgroundType.Album, stream);
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