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
using YARG.Util;

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
        [SuppressMessage("ReSharper", "All")]
        public class SourceIndex
        {
            public string type;
            public Source[] sources;
        }

        [Serializable]
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
            private readonly string _icon;
            private readonly Dictionary<string, string> _names;

            public SourceType Type { get; private set; }
            public bool IsFromBase { get; private set; }

            private bool _isLoadingIcon;
            private Sprite _iconCache;

            public ParsedSource(string icon, Dictionary<string, string> names, SourceType type, bool isFromBase)
            {
                _icon = icon;
                _names = names;
                Type = type;
                IsFromBase = isFromBase;
            }

            public string GetDisplayName()
            {
                return _names["en-US"];
            }

            public async UniTask<Sprite> GetIcon()
            {
                if (_iconCache != null)
                {
                    return _iconCache;
                }

                if (_isLoadingIcon)
                {
                    await UniTask.WaitUntil(() => !_isLoadingIcon);
                }
                else
                {
                    _isLoadingIcon = true;

                    // Look for the icon file in the different folders
                    string imagePath = null;
                    foreach (var type in SourceTypes)
                    {
                        string path = Path.Combine(SourcesFolder, SOURCE_REPO_FOLDER, type, "icons", $"{_icon}.png");
                        if (File.Exists(path))
                        {
                            imagePath = path;
                            break;
                        }
                    }

                    if (imagePath == null)
                    {
                        Debug.LogWarning($"Failed to find source icon `{_icon}`! Does it exist?");
                        return null;
                    }

                    var texture = await TextureLoader.LoadWithMips(imagePath);
                    texture.mipMapBias = -0.5f;

                    if (texture == null)
                    {
                        Debug.LogWarning($"Failed to load texture at `{imagePath}`!");
                        return null;
                    }

                    _iconCache = Sprite.Create(texture,
                        new Rect(0, 0, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f));
                    _isLoadingIcon = false;
                }

                return _iconCache;
            }

            public string GetIconURL()
            {
                if (IsFromBase)
                {
                    return RAW_ICON_URL + $"base/icons/{_icon}.png";
                }

                return RAW_ICON_URL + $"extra/icons/{_icon}.png";
            }
        }

        public static string SourcesFolder => Path.Combine(PathHelper.StreamingAssetsPath, "sources");

        public const string SOURCE_REPO_FOLDER = "OpenSource-master";

        private const string SOURCE_COMMIT_URL =
            "https://api.github.com/repos/YARC-Official/OpenSource/commits?per_page=1";

        private const string RAW_ICON_URL = "https://raw.githubusercontent.com/YARC-Official/OpenSource/master/";

        public const string SOURCE_ZIP_URL =
            "https://github.com/YARC-Official/OpenSource/archive/refs/heads/master.zip";

        private static readonly string[] SourceTypes =
        {
            "base", "extra"
        };

        private static Dictionary<string, ParsedSource> _sources = new();

        public static async UniTask LoadSources(Action<string> updateText)
        {
            await DownloadSources(updateText);

            updateText("Reading sources...");
            await UniTask.RunOnThreadPool(ReadSources);
        }

        public static async UniTask DownloadSources(Action<string> updateText)
        {
            // Create the sources folder if it doesn't exist
            Directory.CreateDirectory(SourcesFolder);

            // Look for the current version
            updateText("Checking version...");
            string currentVersion = null;
            try
            {
                currentVersion = await File.ReadAllTextAsync(Path.Combine(SourcesFolder, "version.txt"));
            }
            catch (Exception e)
            {
                Debug.LogWarning("Failed to get current song source version. Skipping.");
                Debug.LogException(e);
            }

            // Look for new version
            updateText("Looking for new version...");
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
                Debug.LogWarning("Failed to get newest song source version. Skipping.");
                Debug.LogException(e);
            }

            // If we failed to find the newest version, finish
            if (newestVersion == null)
            {
                return;
            }

            // If up to date, finish
            if (newestVersion == currentVersion)
            {
                return;
            }

            // Otherwise, update!
            try
            {
                // Download
                updateText("Downloading new version...");
                string zipPath = Path.Combine(SourcesFolder, "update.zip");
                using (var client = new WebClient())
                {
                    await UniTask.RunOnThreadPool(() => { client.DownloadFile(SOURCE_ZIP_URL, zipPath); });
                }

                // Delete the old folder
                var repoDir = Path.Combine(SourcesFolder, SOURCE_REPO_FOLDER);
                if (Directory.Exists(repoDir))
                {
                    Directory.Delete(repoDir, true);
                }

                // Extract the base and extras folder
                updateText("Extracting new version...");
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
                foreach (var file in Directory.GetFiles(repoDir))
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
                Debug.LogError("Failed to download newest song source version.");
                Debug.LogException(e);
            }
        }

        public static void ReadSources()
        {
            foreach (var index in SourceTypes)
            {
                try
                {
                    var indexPath = Path.Combine(SourcesFolder, SOURCE_REPO_FOLDER, index, "index.json");
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
                        }, sources.type == "base");

                        foreach (var id in source.ids)
                        {
                            _sources.Add(id, parsed);
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to read song source index.json for `{index}`!");
                    Debug.LogException(e);
                }
            }
        }

        public static ParsedSource GetSource(string id)
        {
            if (_sources.TryGetValue(id, out var parsedSource))
            {
                return parsedSource;
            }

            return _sources["$DEFAULT$"];
        }

        public static string SourceToGameName(string id) => GetSource(id).GetDisplayName();
        public static async UniTask<Sprite> SourceToIcon(string id) => await GetSource(id).GetIcon();
    }
}