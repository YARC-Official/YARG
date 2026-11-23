using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using YARG.Helpers;
using YARG.Settings;
using YARG.Core.Song;
using YARG.Core.Venue;
using YARG.Core.IO;
using YARG.Core.Logging;

namespace YARG.Venue
{
    public enum VenueSource
    {
        Global,
        Song,
        Addressable,
        Default,
    }

    public readonly struct LocalGlobalVenueCandidate
    {
        public string         FilePath { get; }
        public BackgroundType Type     { get; }

        // Identifies the venue across song loads without depending on how it was picked
        public string CacheKey => "file:" + FilePath;

        public LocalGlobalVenueCandidate(string filePath, BackgroundType type)
        {
            // Normalized so a venue picked from the launcher folder compares equal to the same file
            // reached through a pinned identifier
            FilePath = Path.GetFullPath(filePath);
            Type = type;
        }
    }

    public static class VenueLoader
    {
        private static readonly string[] _videoExtensions = { "*.mp4", "*.mov", "*.webm" };

        private static readonly string[] _imageExtensions = { "*.png", "*.jpg", "*.jpeg" };

        private static readonly string _venueFolder = Path.Combine(PathHelper.PersistentDataPath, "venue");
        private static readonly string _defaultVenue = Path.Combine(Application.streamingAssetsPath, "venue", "default.yarground");

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
        public static BackgroundResult? LoadPerSongBackground(SongEntry song)
#nullable disable
        {
            if (song == null || SettingsManager.Settings.DisablePerSongBackgrounds.Value)
            {
                return null;
            }

            try
            {
                return song.LoadBackground(SettingsManager.Settings.CensorMatureContent.Value);
            }
            catch (Exception ex)
            {
                YargLogger.LogWarning($"Failed to load per-song background: {ex.Message}");
                return null;
            }
        }

        public static List<LocalGlobalVenueCandidate> GetGlobalLocalCandidates()
        {
            var candidates = new List<LocalGlobalVenueCandidate>();
            var venueFolder = VenueFolder;

            var yargrounds = new List<string>(
                Directory.EnumerateFiles(venueFolder, "*.yarground", PathHelper.SafeSearchOptions));

            var previewSiblings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var yarground in yargrounds)
            {
                // Recording every yarground, not just the enabled ones, keeps a hidden venue's preview image
                // from being promoted into a standalone image background
                previewSiblings.Add(GetSiblingKey(yarground));
            }

            foreach (var yarground in yargrounds)
            {
                var info = VenueRegistry.GetLocalVenueInfo(VenueEntrySource.LocalFile, venueFolder, yarground);
                if (VenueRegistry.IsHidden(info))
                {
                    continue;
                }

                candidates.Add(new LocalGlobalVenueCandidate(yarground, BackgroundType.Yarground));
            }

            var launcherVenueFolder = PathHelper.VenuePath;
            if (!string.IsNullOrEmpty(launcherVenueFolder) && Directory.Exists(launcherVenueFolder))
            {
                // The launcher only downloads yarground files
                foreach (var yarground in Directory.EnumerateFiles(launcherVenueFolder, "*.yarground",
                    PathHelper.SafeSearchOptions))
                {
                    var info = VenueRegistry.GetLocalVenueInfo(VenueEntrySource.LauncherFile, launcherVenueFolder,
                        yarground);
                    if (VenueRegistry.IsHidden(info))
                    {
                        continue;
                    }

                    candidates.Add(new LocalGlobalVenueCandidate(yarground, BackgroundType.Yarground));
                }
            }

            foreach (var extension in _videoExtensions)
            {
                foreach (var filePath in Directory.EnumerateFiles(venueFolder, extension, PathHelper.SafeSearchOptions))
                {
                    candidates.Add(new LocalGlobalVenueCandidate(filePath, BackgroundType.Video));
                }
            }

            foreach (var extension in _imageExtensions)
            {
                foreach (var filePath in Directory.EnumerateFiles(venueFolder, extension, PathHelper.SafeSearchOptions))
                {
                    if (previewSiblings.Contains(GetSiblingKey(filePath)))
                    {
                        continue;
                    }

                    candidates.Add(new LocalGlobalVenueCandidate(filePath, BackgroundType.Image));
                }
            }

            return candidates;
        }

#nullable enable
        public static BackgroundResult? OpenLocalCandidate(LocalGlobalVenueCandidate candidate)
#nullable disable
        {
            try
            {
                switch (candidate.Type)
                {
                    case BackgroundType.Image:
                        var image = YARGImage.Load(candidate.FilePath);
                        if (image == null)
                        {
                            return null;
                        }

                        return new BackgroundResult(image);
                    case BackgroundType.Video:
                        return new BackgroundResult(BackgroundType.Video, File.OpenRead(candidate.FilePath));
                    case BackgroundType.Yarground:
                        return new BackgroundResult(BackgroundType.Yarground, File.OpenRead(candidate.FilePath));
                    default:
                        return null;
                }
            }
            catch (Exception ex)
            {
                YargLogger.LogWarning($"Failed to open venue background {candidate.FilePath}: {ex.Message}");
                return null;
            }
        }

#nullable enable
        public static BackgroundResult? LoadDefaultVenue()
#nullable disable
        {
            if (SettingsManager.Settings.DisableDefaultBackground.Value)
            {
                return null;
            }

            if (!File.Exists(_defaultVenue))
            {
                YargLogger.LogWarning("Default venue not found. Build error?");
                return null;
            }

            try
            {
                return new BackgroundResult(BackgroundType.Yarground, File.OpenRead(_defaultVenue));
            }
            catch (Exception ex)
            {
                YargLogger.LogWarning($"Failed to open the default venue: {ex.Message}");
                return null;
            }
        }

        private static string GetSiblingKey(string filePath)
        {
            return Path.Combine(Path.GetDirectoryName(filePath) ?? string.Empty,
                Path.GetFileNameWithoutExtension(filePath));
        }
    }
}
