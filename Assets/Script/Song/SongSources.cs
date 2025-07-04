using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using YARG.Core.IO;
using YARG.Core.Logging;
using YARG.Core.Song;
using YARG.Helpers;
using YARG.Helpers.Extensions;
using YARG.Settings.Customization;

namespace YARG.Song
{
    public static class SongSources
    {
        public enum SourceType
        {
            Custom,
            Game,
            Charter,
            RB,
            GH
        }

        [Serializable]
        // This is a serialized class; naming conventions are JSON's, not C#'s
        [SuppressMessage("ReSharper", "All")]
        public class SourceIndex
        {
            public string type;
            public Source[] sources;
        }

        [Serializable]
        // This is a serialized class; naming conventions are JSON's, not C#'s
        [SuppressMessage("ReSharper", "All")]
        public class Source
        {
            public string[] ids;
            public Dictionary<string, string> names;
            public string icon;
            public string type;
        }

        public class ParsedSource
        {
            public string IconName { get; set; }
            private readonly Dictionary<string, string> _names;
#nullable enable
            private Sprite? _sprite;
#nullable disable

            public readonly SourceType Type;

#nullable enable
            public Sprite? Sprite => _sprite;
#nullable disable

            public ParsedSource(string icon, Dictionary<string, string> names, SourceType type)
            {
                IconName = icon;
                _names = names;
                Type = type;
            }

            public string GetDisplayName()
            {
                return _names["en-US"];
            }

            public async void LoadSprite()
            {
                if (_sprite != null)
                {
                    return;
                }

#nullable enable
                // Look for the icon file in the different folders
                Texture2D? texture = null;
#nullable disable
                foreach (var root in SourceRoots)
                {
                    string file = Path.Combine(root, $"{IconName}.png");
                    if (File.Exists(file))
                    {
                        using var image = await UniTask.RunOnThreadPool(() => YARGImage.Load(file));
                        if (image == null)
                        {
                            YargLogger.LogFormatWarning("Failed to load source icon `{0}`!", file);
                            return;
                        }
                        texture = image.LoadTexture(true);
                        texture.mipMapBias = -0.5f;
                        break;
                    }
                }

                if (texture == null)
                {
                    YargLogger.LogFormatWarning("Failed to find source icon `{0}`! Does it exist?", IconName);
                    return;
                }

                _sprite = Sprite.Create(texture,
                    new Rect(0, 0, texture.width, -texture.height),
                    new Vector2(0.5f, 0.5f));
            }
        }

        public const string SOURCE_REPO_FOLDER = "OpenSource-master";
#if UNITY_EDITOR
        // The editor does not track the contents of folders that end in ~,
        // so use this to prevent Unity from stalling due to importing freshly-downloaded sources
        public static readonly string SourcesFolder = Path.Combine(PathHelper.StreamingAssetsPath, "sources~");
#else
        public static readonly string SourcesFolder = Path.Combine(PathHelper.StreamingAssetsPath, "sources");
#endif
        public static readonly string CustomSourcesFolder = Path.Combine(CustomContentManager.CustomizationDirectory, "icons");

        private static readonly string[] SourceTypes =
        {
            "base", "extra"
        };

        private static readonly string[] SourceRoots =
        {
            CustomSourcesFolder, // Prioritize user-supplied sources
            Path.Combine(SourcesFolder, SOURCE_REPO_FOLDER, "base", "icons"),
            Path.Combine(SourcesFolder, SOURCE_REPO_FOLDER, "extra", "icons"),
        };

        private const string SOURCE_COMMIT_URL =
            "https://api.github.com/repos/YARC-Official/OpenSource/commits?per_page=1";

        public const string SOURCE_ZIP_URL =
            "https://github.com/YARC-Official/OpenSource/archive/refs/heads/master.zip";

        private const string DEFAULT_KEY = "$DEFAULT$";

        private static readonly Dictionary<string, ParsedSource> _sources = new(StringComparer.OrdinalIgnoreCase);
        private static ParsedSource _default;
        public static ParsedSource Default => _default;

        public static async UniTask LoadSources(LoadingContext context)
        {
            if (!GlobalVariables.OfflineMode)
            {
                await DownloadSources(context);
            }

            context.SetSubText("Reading song sources...");
            ReadSources();
        }

        public static void LoadSprites(LoadingContext context)
        {
            context.SetLoadingText("Loading source icons...");

            _default.LoadSprite();
            foreach (var node in SongContainer.Sources)
            {
                if (_sources.TryGetValue(node.Key, out var source))
                {
                    source.LoadSprite();
                }
            }
        }

        private static async UniTask DownloadSources(LoadingContext context)
        {
            context.SetLoadingText("Loading song sources...");

            // Create the sources folder if it doesn't exist
            Directory.CreateDirectory(SourcesFolder);

            // Look for the current version
            context.SetSubText("Checking version...");
            string sourceVersionPath = Path.Combine(SourcesFolder, "version.txt");
            string currentVersion = null;
            try
            {
                if (File.Exists(sourceVersionPath))
                    currentVersion = await File.ReadAllTextAsync(sourceVersionPath);
            }
            catch (Exception e)
            {
                YargLogger.LogException(e, "Failed to get current song source version.");
            }

            // Look for new version
            context.SetSubText("Looking for new version...");
            string newestVersion = null;
            try
            {
                // Retrieve sources file
                var request = (HttpWebRequest) WebRequest.Create(SOURCE_COMMIT_URL);
                request.UserAgent = "YARG";
                request.Timeout = 2500;

                // Send the request and wait for the response
                using var response = await request.GetResponseAsync();
                using var reader = new StreamReader(response.GetResponseStream()!, Encoding.UTF8);

                // Read the JSON
                var json = JArray.Parse(await reader.ReadToEndAsync());
                newestVersion = json[0]["sha"]!.ToString();
            }
            catch (Exception e)
            {
                YargLogger.LogException(e, "Failed to get newest song source version. Skipping.");
            }

            // If we failed to find the newest version, finish
            if (newestVersion == null)
            {
                return;
            }

            // If up to date, finish
            var repoDir = Path.Combine(SourcesFolder, SOURCE_REPO_FOLDER);
            if (newestVersion == currentVersion && Directory.Exists(repoDir))
            {
                return;
            }

            // Otherwise, update!
            try
            {
                // Download
                context.SetSubText("Downloading new version...");
                string zipPath = Path.Combine(SourcesFolder, "update.zip");
                using (var client = new WebClient())
                {
                    await UniTask.RunOnThreadPool(() => { client.DownloadFile(SOURCE_ZIP_URL, zipPath); });
                }

                // Delete the old folder
                if (Directory.Exists(repoDir))
                {
                    Directory.Delete(repoDir, true);
                }

                // Extract the base and extras folder
                context.SetSubText("Extracting new version...");
                ZipFile.ExtractToDirectory(zipPath, SourcesFolder);

                // Delete the random folders
                var ignoreFolder = Path.Combine(repoDir, "ignore");
                if (Directory.Exists(ignoreFolder))
                {
                    Directory.Delete(ignoreFolder, true);
                }

                var githubFolder = Path.Combine(repoDir, ".github");
                if (Directory.Exists(githubFolder))
                {
                    Directory.Delete(githubFolder, true);
                }

                // Delete the random files
                foreach (var file in Directory.EnumerateFiles(repoDir))
                {
                    File.Delete(file);
                }

                // Create the version txt
                await File.WriteAllTextAsync(Path.Combine(SourcesFolder, "version.txt"), newestVersion);

                // Delete the zip
                File.Delete(zipPath);
            }
            catch (Exception e)
            {
                YargLogger.LogException(e, "Failed to download newest song source version.");
            }
        }

        private static void ReadSources()
        {
            // Read custom sources - they are read first so user replacements are prioritized
            // Create the folder if it doesn't exist
            Directory.CreateDirectory(CustomSourcesFolder);
            // Read index.json if it exists; otherwise fallback to reading PNGs individually later on
            string customIndexPath = Path.Combine(CustomSourcesFolder, "index.json");
            bool customSourcesRead = false;
            if (File.Exists(customIndexPath))
            {
                ReadIndexPath(customIndexPath);
                customSourcesRead = true;
            }

            // Read regular sources
            foreach (var index in SourceTypes)
            {
                try
                {
                    var indexPath = Path.Combine(SourcesFolder, SOURCE_REPO_FOLDER, index, "index.json");
                    ReadIndexPath(indexPath);
                }
                catch (Exception e)
                {
                    YargLogger.LogException(e, $"Failed to read song source index.json for `{index}`!");

                    // If we failed when fetching "base", something went wrong.
                    if (index == "base")
                    {
                        YargLogger.LogError("Skipping and creating a backup source.");
                        CreateBackupSource();
                        return;
                    }
                }
            }

            // Read individual PNGs (this is done after reading regular sources, so their icons can be replaced if needed)
            if (!customSourcesRead)
            {
                PathHelper.SafeEnumerateFiles(CustomSourcesFolder, "*.png", true, (path) =>
                {
                    // Filename will be used for all values (id, name, icon path).
                    string icon = Path.GetFileNameWithoutExtension(path);
                    var names = new Dictionary<string, string> {
                        { "en-US", icon }
                    };
                    var parsed = new ParsedSource(icon, names, SourceType.Custom);
                    if (_sources.TryAdd(icon, parsed))
                    {
                        if (icon == DEFAULT_KEY)
                        {
                            _default = parsed;
                        }
                    }
                    else // Source already exists; override its icon only (preserving metadata)
                    {
                        _sources[icon].IconName = icon;
                    }
                    return true;
                });
            }
        }

        private static void ReadIndexPath(String indexPath)
        {
            var sources = JsonConvert.DeserializeObject<SourceIndex>(File.ReadAllText(indexPath));

            foreach (var source in sources.sources)
            {
                var parsed = new ParsedSource(source.icon, source.names, source.type switch
                {
                    "game"    => SourceType.Game,
                    "charter" => SourceType.Charter,
                    "rb"      => SourceType.RB,
                    "gh"      => SourceType.GH,
                    _         => SourceType.Custom
                });

                foreach (string id in source.ids)
                {
                    if (_sources.TryAdd(id, parsed) && id == DEFAULT_KEY)
                    {
                        _default = parsed;
                    }
                }
            }
        }

        private static void CreateBackupSource()
        {
            // If this method is called, the "custom" icon will likely not exist,
            // however, the icon loader deals with this.
            _default = new ParsedSource("custom", new()
            {
                { "en-US", "Unknown" }
            }, SourceType.Custom);
            _sources.Add(DEFAULT_KEY, _default);
        }

        public static bool TryGetSource(string id, out ParsedSource parsedSource)
        {
            return _sources.TryGetValue(id, out parsedSource);
        }

        public static ParsedSource GetSourceOrDefault(string id)
        {
            if (!TryGetSource(id, out var parsedSource))
            {
                parsedSource = _default;
            }
            return parsedSource;
        }

        public static string SourceToGameName(string id) => GetSourceOrDefault(id).GetDisplayName();

        public static Sprite SourceToIcon(string id) => GetSourceOrDefault(id).Sprite;
    }
}