using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using YARG.Core.Logging;

namespace YARG.Venue
{
#nullable enable
    public readonly struct LocalVenueMetadataResult
    {
        public string? DisplayName      { get; }
        public string? Author           { get; }
        public string? Description      { get; }
        public string? PreviewImagePath { get; }
        public bool    HasMetadata      { get; }

        public LocalVenueMetadataResult(string? displayName, string? author, string? description,
            string? previewImagePath, bool hasMetadata)
        {
            DisplayName = displayName;
            Author = author;
            Description = description;
            PreviewImagePath = previewImagePath;
            HasMetadata = hasMetadata;
        }
    }
#nullable disable

    // Sidecar reading lives here so a future single-file .yarground container only has to change this class
    public static class LocalVenueMetadataReader
    {
        private static readonly string[] PREVIEW_EXTENSIONS =
        {
            ".png", ".jpg", ".jpeg",
        };

        public static LocalVenueMetadataResult ReadMetadata(string yargroundPath)
        {
            var displayName = Path.GetFileNameWithoutExtension(yargroundPath);
            string author = null;
            string description = null;
            var hasMetadata = false;

            var jsonPath = Path.ChangeExtension(yargroundPath, ".json");
            try
            {
                if (File.Exists(jsonPath))
                {
                    var json = JsonConvert.DeserializeObject<SidecarJson>(File.ReadAllText(jsonPath));
                    if (json != null)
                    {
                        if (!string.IsNullOrWhiteSpace(json.Name))
                        {
                            displayName = json.Name.Trim();
                            hasMetadata = true;
                        }

                        if (!string.IsNullOrWhiteSpace(json.Author))
                        {
                            author = json.Author.Trim();
                            hasMetadata = true;
                        }

                        if (!string.IsNullOrWhiteSpace(json.Description))
                        {
                            description = json.Description.Trim();
                            hasMetadata = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // A broken sidecar must never make the venue itself unloadable
                YargLogger.LogWarning($"Failed to read venue metadata {jsonPath}: {ex.Message}");
                displayName = Path.GetFileNameWithoutExtension(yargroundPath);
                author = null;
                description = null;
                hasMetadata = false;
            }

            return new LocalVenueMetadataResult(displayName, author, description,
                FindPreviewImagePath(yargroundPath), hasMetadata);
        }

        private static string FindPreviewImagePath(string yargroundPath)
        {
            var directory = Path.GetDirectoryName(yargroundPath);
            if (string.IsNullOrEmpty(directory))
            {
                directory = string.Empty;
            }

            var stem = Path.GetFileNameWithoutExtension(yargroundPath);
            foreach (var extension in PREVIEW_EXTENSIONS)
            {
                var candidate = Path.Combine(directory, stem + extension);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

#nullable enable
        public static async UniTask<Texture2D?> LoadPreviewTextureAsync(string imagePath,
            CancellationToken cancellationToken)
#nullable disable
        {
            if (string.IsNullOrEmpty(imagePath))
            {
                return null;
            }

            byte[] bytes;
            try
            {
                var (cancelled, loaded) = await UniTask.RunOnThreadPool(
                    () => File.ReadAllBytes(imagePath),
                    cancellationToken: cancellationToken).SuppressCancellationThrow();

                if (cancelled)
                {
                    return null;
                }

                bytes = loaded;
            }
            catch (Exception ex)
            {
                YargLogger.LogWarning($"Failed to read venue preview image {imagePath}: {ex.Message}");
                return null;
            }

            if (bytes == null || bytes.Length == 0)
            {
                return null;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return null;
            }

            // Texture creation must happen on the main thread, which we are back on after the thread pool read
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!ImageConversion.LoadImage(texture, bytes, true))
            {
                UnityEngine.Object.Destroy(texture);
                return null;
            }

            return texture;
        }

        private sealed class SidecarJson
        {
            public string Name;
            public string Author;
            public string Description;
        }
    }
}
