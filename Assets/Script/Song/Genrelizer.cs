using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using YARG.Core.Logging;
using YARG.Core.Song;
using YARG.Helpers;

namespace YARG.Song
{
    public static partial class Genrelizer
    {
        [Serializable]
        // This is a serialized class; naming conventions are JSON's, not C#'s
        [SuppressMessage("ReSharper", "All")]
        public class SubgenreMapping
        {
            public string genre;
            public string capitalized;
            public Dictionary<string, string>? localizations;

            
            public string Genre => GetLocalizedGenre(genre);
            public string Subgenre => localizations is null ? capitalized : localizations.GetValueOrDefault(Localization.LocalizationManager.CultureCode, capitalized);
        }

        public static void GenrelizeAll(SongCache cache)
        {
            // If Genrelizer data has failed, fall back to parsing literally
            if (_genreAliases.Count + _subgenreAliases.Count + _subgenreMappings.Count == 0)
            {
                DegenrelizeAll(cache);
                return;
            }

            foreach (var list in cache.Entries)
            {
                foreach (var songEntry in list.Value)
                {
                    (songEntry.Genre, songEntry.Subgenre) = GetGenresOrDefault(songEntry.Genre, songEntry.Subgenre);
                }
            }
        }

        public static void DegenrelizeAll(SongCache cache)
        {
            foreach (var list in cache.Entries)
            {
                foreach (var songEntry in list.Value)
                {
                    (songEntry.Genre, songEntry.Subgenre) = (songEntry.RawGenre, songEntry.RawSubgenre);
                }
            }
        }

        private static string GetLocalizedGenre(string genre)
        {
            if (genre is null)
            {
                // This can only happen if Genrelizer's data is malformed
                return Localization.Localize.Key("Menu.MusicLibrary.Genre.UnknownGenre");
            }
            return Localization.Localize.Key("Menu.MusicLibrary.Genre", GENRE_LOCALIZATION_KEYS.GetValueOrDefault(genre));
        }

        private const string GENRE_COMMIT_URL =
            "https://api.github.com/repos/YARC-Official/Genrelizer/commits?per_page=1";

        private const string GENRE_ZIP_URL =
            "https://github.com/YARC-Official/Genrelizer/archive/refs/heads/master.zip";

        public const string GENRE_REPO_FOLDER = "Genrelizer-master";

        private const string GENRE_ALIASES_FILENAME = "genreAliases.json";
        private const string SUBGENRE_ALIASES_FILENAME = "subgenreAliases.json";
        private const string SUBGENRE_MAPPINGS_FILENAME = "subgenreMappings.json";

#if UNITY_EDITOR
        // The editor does not track the contents of folders that end in ~,
        // so use this to prevent Unity from stalling due to importing freshly-downloaded sources
        public static readonly string GenresFolder = Path.Combine(PathHelper.StreamingAssetsPath, "genres~");
#else
        public static readonly string GenresFolder = Path.Combine(PathHelper.StreamingAssetsPath, "genres");
#endif

        private static Dictionary<string, string> _genreAliases = new(StringComparer.OrdinalIgnoreCase);
        private static Dictionary<string, string> _subgenreAliases = new(StringComparer.OrdinalIgnoreCase);
        private static Dictionary<string, SubgenreMapping> _subgenreMappings = new(StringComparer.OrdinalIgnoreCase);

        public static async UniTask LoadGenreMappings(LoadingContext context)
        {
            if (!GlobalVariables.OfflineMode)
            {
                await DownloadGenreMappings(context);
            }

            context.SetSubText("Loading genre mappings...");
            ReadGenreAliases();
            ReadSubgenreAliases();
            ReadSubgenreMappings();
        }

        private static async UniTask DownloadGenreMappings(LoadingContext context)
        {
            context.SetLoadingText("Downloading genre mappings...");

            // Create the sources folder if it doesn't exist
            Directory.CreateDirectory(GenresFolder);

            context.SetSubText("Checking version...");
            string genreVersionPath = Path.Combine(GenresFolder, "version.txt");
            string currentVersion = null;
            try
            {
                if (File.Exists(genreVersionPath))
                    currentVersion = await File.ReadAllTextAsync(genreVersionPath);
            }
            catch (Exception e)
            {
                YargLogger.LogException(e, "Failed to get current song genre version.");
            }

            // Look for new version
            context.SetSubText("Looking for new version...");
            string newestVersion = null;
            try
            {
                // Retrieve sources file
                var request = (HttpWebRequest) WebRequest.Create(GENRE_COMMIT_URL);
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
                YargLogger.LogException(e, "Failed to get newest song genre version. Skipping.");
            }

            // If we failed to find the newest version, finish
            if (newestVersion == null)
            {
                return;
            }

            // If up to date, finish
            var repoDir = Path.Combine(GenresFolder, GENRE_REPO_FOLDER);
            if (newestVersion == currentVersion && Directory.Exists(repoDir))
            {
                return;
            }

            // Otherwise, update!
            try
            {
                // Download
                context.SetSubText("Downloading new version...");
                string zipPath = Path.Combine(GenresFolder, "update.zip");
                using (var client = new WebClient())
                {
                    await UniTask.RunOnThreadPool(() => { client.DownloadFile(GENRE_ZIP_URL, zipPath); });
                }

                // Delete the old folder
                if (Directory.Exists(repoDir))
                {
                    Directory.Delete(repoDir, true);
                }

                // Extract the base and extras folder
                context.SetSubText("Extracting new version...");
                ZipFile.ExtractToDirectory(zipPath, GenresFolder);

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
                    if (!file.EndsWith(".json"))
                    {
                        File.Delete(file);
                    }
                }

                // Create the version txt
                await File.WriteAllTextAsync(Path.Combine(GenresFolder, "version.txt"), newestVersion);

                // Delete the zip
                File.Delete(zipPath);
            }
            catch (Exception e)
            {
                YargLogger.LogException(e, "Failed to download newest song genre version.");
            }
        }

        private static void ReadGenreAliases()
        {
            var path = Path.Combine(GenresFolder, GENRE_REPO_FOLDER, GENRE_ALIASES_FILENAME);
            var mappings = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(path));
            foreach (var key in mappings.Keys)
            {
                _genreAliases.Add(key, mappings[key]);
            }
        }

        private static void ReadSubgenreAliases()
        {
            var path = Path.Combine(GenresFolder, GENRE_REPO_FOLDER, SUBGENRE_ALIASES_FILENAME);
            var mappings = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(path));
            foreach (var key in mappings.Keys)
            {
                _subgenreAliases.Add(key, mappings[key]);
            }
        }

        private static void ReadSubgenreMappings()
        {
            var path = Path.Combine(GenresFolder, GENRE_REPO_FOLDER, SUBGENRE_MAPPINGS_FILENAME);
            var mappings = JsonConvert.DeserializeObject<Dictionary<string, SubgenreMapping>>(File.ReadAllText(path));
            foreach (var key in mappings.Keys)
            {
                _subgenreMappings.Add(key, mappings[key]);
            }
        }

        public static (SortString genre, SortString subgenre) GetGenresOrDefault(string? rawGenre, string? rawSubgenre)
        {
            if (string.IsNullOrEmpty(rawGenre))
            {
                // If neither value is provided, return nothing
                if (string.IsNullOrEmpty(rawSubgenre))
                {
                    return (
                        new(Localization.Localize.Key("Menu.MusicLibrary.Genre.UnknownGenre")),
                        new(string.Empty)
                    );
                }

                // If only a subgenre is provided (not expected), treat it as the genre
                return HandleLoneGenre(rawSubgenre);
            }

            if (string.IsNullOrEmpty(rawSubgenre))
            {
                return HandleLoneGenre(rawGenre);
            }

            return HandleGenreSubgenrePair(rawGenre, rawSubgenre);
        }

        private static (SortString genre, SortString subgenre) HandleLoneGenre(string rawGenre) {
            // Apply any genre alias, if present
            var processedGenre = _genreAliases.GetValueOrDefault(rawGenre, rawGenre);

            if (GENRE_LOCALIZATION_KEYS.ContainsKey(processedGenre))
            {
                // We have an official genre, so localize it and return it with no subgenre
                return (
                    new(Localization.Localize.Key("Menu.MusicLibrary.Genre", GENRE_LOCALIZATION_KEYS.GetValueOrDefault(processedGenre))),
                    new(string.Empty)
                );
            }

            // This isn't an official genre, so we're going to use it as a subgenre

            // First, apply a subgenre alias, if present
            var subgenre = _subgenreAliases.GetValueOrDefault(rawGenre, rawGenre);

            // Is this a known subgenre?
            if (_subgenreMappings.ContainsKey(subgenre))
            {
                var mapping = _subgenreMappings[subgenre];

                return (
                    new(Localization.Localize.Key("Menu.MusicLibrary.Genre", GENRE_LOCALIZATION_KEYS.GetValueOrDefault(mapping.Genre))),
                    new(mapping.Subgenre)
                );
            }

            // Not a known subgenre. If it contains any slashes, there are a couple more things we can try
            if (subgenre.Contains('/'))
            {
                /* Usually, when a genre name contains one or more slashes, it fits one of these two patterns:
                 *
                 * A) List of several, often unrelated, genre names (e.g. "Hard Rock/Heavy Metal" or 
                 *      "Funk / Disco / Polka")
                 * B) Single genre with multiple adjectives or modifiers (e.g. "Melodic/Neoclassical Metal"
                 *      or "Smooth/Cool/Soft Jazz")
                 *
                 * In Pattern A, we can pick one genre and run with it. It's reasonable to assume that, if any
                 * of the genres stand out as the foremost description of the song, it's probably the first one
                 * in the list. Thus, we'll try matching the content of the string that comes before the first
                 * slash. In the Pattern A examples, this would lead us to "Hard Rock" (a genre in its own right)
                 * and "Funk" (a subgenre that maps to R&B/Soul/Funk), respectively.
                 * 
                 * Applying the same logic to Pattern B strings doesn't yield results though: in the given examples,
                 * we would wind up with "Melodic" and "Smooth", neither of which is a genre of any kind. In this case,
                 * we care more about the noun at the end of the string ("Metal" and "Jazz"). So if Pattern A didn't
                 * yield results, we'll try matching the content that comes after the *last* string. This yields
                 * "Neoclassical Metal" and "Soft Jazz", which are each mapped subgenres.
                 */

                // Attempt Pattern A
                var beforeFirstSlash = subgenre[0..subgenre.IndexOf('/')].TrimEnd();
                var beforeFirstSlashAsGenre = _genreAliases.GetValueOrDefault(beforeFirstSlash, beforeFirstSlash);
                if (GENRE_LOCALIZATION_KEYS.ContainsKey(beforeFirstSlashAsGenre)) {
                    return (
                        new(Localization.Localize.Key("Menu.MusicLibrary.Genre", GENRE_LOCALIZATION_KEYS.GetValueOrDefault(beforeFirstSlashAsGenre))),
                        new(_sanitize(subgenre))
                    );
                }
                var beforeFirstSlashAsSubgenre = _subgenreAliases.GetValueOrDefault(beforeFirstSlash, beforeFirstSlash);
                if (_subgenreMappings.ContainsKey(beforeFirstSlashAsSubgenre)) {
                    var mapping = _subgenreMappings[beforeFirstSlashAsSubgenre];
                    return (
                        new (mapping.Genre),
                        new (mapping.Subgenre)
                    );
                }

                // Attempt Pattern B
                var afterLastSlash = subgenre.Substring(subgenre.LastIndexOf('/') + 1).TrimStart();
                var afterLastSlashAsGenre = _genreAliases.GetValueOrDefault(afterLastSlash, afterLastSlash);
                if (GENRE_LOCALIZATION_KEYS.ContainsKey(afterLastSlashAsGenre))
                {
                    return (
                        new(Localization.Localize.Key("Menu.MusicLibrary.Genre", GENRE_LOCALIZATION_KEYS.GetValueOrDefault(afterLastSlashAsGenre))),
                        new(_sanitize(subgenre))
                    );
                }
                var afterLastSlashAsSubgenre = _subgenreAliases.GetValueOrDefault(afterLastSlash, afterLastSlash);
                if (_subgenreMappings.ContainsKey(afterLastSlashAsSubgenre))
                {
                    var mapping = _subgenreMappings[afterLastSlashAsSubgenre];
                    return (
                        new(mapping.Genre),
                        new(mapping.Subgenre)
                    );
                }
            }

            if (subgenre.Contains(','))
            {
                /* 
                 * Pattern A can also occur with commas rather than slashes, like "Hard Rock, Heavy Metal", so try
                 * that too. Pattern B generally doesn't appear with commas, so don't bother with that.
                 */
                var beforeFirstComma = subgenre[0..subgenre.IndexOf(',')].TrimEnd();
                var beforeFirstCommaAsGenre = _genreAliases.GetValueOrDefault(beforeFirstComma, beforeFirstComma);
                if (GENRE_LOCALIZATION_KEYS.ContainsKey(beforeFirstCommaAsGenre))
                {
                    return (
                        new(Localization.Localize.Key("Menu.MusicLibrary.Genre", GENRE_LOCALIZATION_KEYS.GetValueOrDefault(beforeFirstCommaAsGenre))),
                        new(_sanitize(subgenre))
                    );
                }
                var beforeFirstCommaAsSubgenre = _subgenreAliases.GetValueOrDefault(beforeFirstComma, beforeFirstComma);
                if (_subgenreMappings.ContainsKey(beforeFirstCommaAsSubgenre))
                {
                    var mapping = _subgenreMappings[beforeFirstCommaAsSubgenre];
                    return (
                        new(mapping.Genre),
                        new(mapping.Subgenre)
                    );
                }
            }

            // We've exhausted all of our options, so default to Other
            return (
                new(Localization.Localize.Key("Menu.MusicLibrary.Genre.Other")),
                new(subgenre)
            );
        }

        private static (SortString genre, SortString subgenre) HandleGenreSubgenrePair(string rawGenre, string rawSubgenre)
        {
            string genre = string.Empty;
            string subgenre = string.Empty;

            // Check if this is a telltale value pair from Magma, for which we have a ready-to-go mapping
            if (MAGMA_MAPPINGS.ContainsKey((rawGenre, rawSubgenre)))
            {
                var magmaMapping = MAGMA_MAPPINGS[(rawGenre, rawSubgenre)];

                // Localize the returned genre directly
                genre = Localization.Localize.Key("Menu.MusicLibrary.Genre", GENRE_LOCALIZATION_KEYS.GetValueOrDefault(magmaMapping.genre));

                // Subgenre localizations and capitalizations come from Genrelizer
                if (magmaMapping.subgenre is not null)
                {
                    var magmaSubgenreMapping = _subgenreMappings[magmaMapping.subgenre];
                    if (magmaSubgenreMapping is not null)
                    {
                        subgenre = magmaSubgenreMapping.Subgenre;
                    } else
                    {
                        // Belt-and-suspenders; we shouldn't have any unrecognized or unsanitized subgenres
                        // in the hardcoded Magma mappings
                        subgenre = _sanitize(magmaMapping.subgenre);
                    }
                }

                return (new(genre), new(subgenre));
            }

            // Apply any available aliases
            var aliasedGenre = _genreAliases.GetValueOrDefault(rawGenre, rawGenre);
            var aliasedSubgenre = _subgenreAliases.GetValueOrDefault(rawSubgenre, rawSubgenre);

            // If the genre and subgenre are the same, discard the subgenre and proceed with only the genre.
            // This includes if either field matches the other after going through that other field's aliases,
            // so Tech Death > Technical Death Metal is a match (both resolve to the Technical Death Metal
            // subgenre) and so is IDM > Braindance (both resolve to the IDM genre)
            var subgenreAsGenre = _genreAliases.GetValueOrDefault(rawSubgenre, rawSubgenre);
            var genreAsSubgenre = _subgenreAliases.GetValueOrDefault(rawGenre, rawGenre);
            if (_sanitize(rawGenre) == _sanitize(rawSubgenre) || aliasedGenre == subgenreAsGenre || genreAsSubgenre == aliasedSubgenre)
            {
                return HandleLoneGenre(aliasedGenre);
            }


            if (GENRE_LOCALIZATION_KEYS.ContainsKey(aliasedGenre))
            {
                // The genre is an official one! Localize it, and then we can move on to the subgenre
                genre = Localization.Localize.Key("Menu.MusicLibrary.Genre", GENRE_LOCALIZATION_KEYS.GetValueOrDefault(aliasedGenre));
            }
            // If the genre is not official, it's probably getting discarded and replaced based on the subgenre mapping.
            // We'll revisit the original genre if the subgenre is also unrecognized

            
            var sanitizedSubgenre = _sanitize(aliasedSubgenre);

            // When the genre is Other, the subgenre can override it
            if (aliasedGenre is OTHER)
            {
                // If the subgenre aliased to a genre, then we can just use that genre (and no subgenre)
                if (subgenreAsGenre is not null)
                {
                    return (new(subgenreAsGenre), new(string.Empty));
                }

                // Otherwise, if the subgenre has a known mapping, then we can use that mapping
                if (_subgenreMappings.ContainsKey(sanitizedSubgenre))
                {
                    var mapping = _subgenreMappings[sanitizedSubgenre];
                    return (new(mapping.Genre), new(mapping.Subgenre));
                }

                // Otherwise, forget it; this really is getting filed under Other
                return (
                    new(Localization.Localize.Key("Menu.MusicLibrary.Genre.Other")),
                    new(sanitizedSubgenre)
                );
            }

            // See if we have a mapping for this subgenre
            if (_subgenreMappings.ContainsKey(sanitizedSubgenre))
            {
                subgenre = _subgenreMappings.GetValueOrDefault(sanitizedSubgenre)?.Subgenre;
            }

            else
            {
                // The subgenre wasn't recognized, so we'll just use it as-is
                subgenre = sanitizedSubgenre;

                // If we still haven't figured out a genre...
                if (string.IsNullOrEmpty(genre))
                {
                    // ...our last resort is to see if treating the original genre
                    // as a subgenre gets us a mapping that we can derive a genre from
                    var genreAsAliasedSubgenre = _subgenreAliases.GetValueOrDefault(rawGenre, rawGenre);
                    if (_subgenreMappings.ContainsKey(genreAsAliasedSubgenre))
                    {
                        // That led us to a mapping! We can use the genre from this mapping as the final genre.
                        genre = _subgenreMappings.GetValueOrDefault(genreAsAliasedSubgenre)?.Genre;
                    }
                    else
                    {
                        // That didn't work either, so just default to Other
                        genre = Localization.Localize.Key("Menu.MusicLibrary.Genre.Other");
                    }
                }
            }

            return (new(genre), new(subgenre));
        }

        private static string _sanitize(string subgenre)
        {
            var textInfo = new CultureInfo(Localization.LocalizationManager.CultureCode).TextInfo;
            return textInfo.ToTitleCase(subgenre.Trim());
        }
    }
}
