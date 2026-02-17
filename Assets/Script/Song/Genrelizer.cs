using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
using UnityEngine.Networking;
using YARG.Core.Logging;
using YARG.Core.Song;
using YARG.Helpers;

namespace YARG.Song
{
    public static partial class Genrelizer
    {
        private static Dictionary<string, Mapping> _mappings = new(StringComparer.OrdinalIgnoreCase);

        public struct Mapping {
            public Mapping(string genre, string subgenre) {
                Genre = new(genre);
                Subgenre = new(subgenre ?? "");
            }

            public SortString Genre { get; }
            public SortString Subgenre { get; }
        }

        public static void GenrelizeAll(SongCache cache)
        {
            // If Genrelizer data has failed, fall back to parsing literally
            if (_mappings.Count == 0)
            {
                DegenrelizeAll(cache);
                return;
            }

            foreach (var list in cache.Entries)
            {
                foreach (var songEntry in list.Value)
                {
                    var mapping = _getGenresOrDefault(songEntry.Genre, songEntry.Subgenre, songEntry.Artist);
                    songEntry.Genre = mapping.Genre;
                    songEntry.Subgenre = mapping.Subgenre;
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

        private static string _getLocalizedGenre(string genre)
        {
            var res = Localization.Localize.Key("Menu.MusicLibrary.Genre", GENRE_LOCALIZATION_KEYS.GetValueOrDefault(genre));
            return res;
        }

        private const string GENRE_COMMIT_URL =
            "https://api.github.com/repos/YARC-Official/Genrelizer/commits?per_page=1";

        private const string GENRE_ZIP_URL =
            "https://github.com/YARC-Official/Genrelizer/archive/refs/heads/master.zip";

        private const string GENRE_REPO_FOLDER = "Genrelizer-master";

        private const string MAPPINGS_FOLDER = "mappings";

#if UNITY_EDITOR
        // The editor does not track the contents of folders that end in ~,
        // so use this to prevent Unity from stalling due to importing freshly-downloaded mappings
        private static readonly string GenresFolder = Path.Combine(PathHelper.StreamingAssetsPath, "genres~");
#else
        public static readonly string GenresFolder = Path.Combine(PathHelper.StreamingAssetsPath, "genres");
#endif

        public static async UniTask LoadGenreMappings(LoadingContext context)
        {
            if (!GlobalVariables.OfflineMode)
            {
                await _downloadGenreMappings(context);
            }

            context.SetSubText("Loading genre mappings...");
            _readGenreMappings();
        }

        private static void _addMapping(string key, Mapping mapping)
        {
            if (_mappings.ContainsKey(key))
            {
                YargLogger.LogError($"Tried to add redundant genre mapping key {key}!");
            } else
            {
                _mappings.Add(key, mapping);
            }
        }

        private static void _readGenreMappings() {
            var mappingsDirectoryPath = System.IO.Path.Combine(GenresFolder, GENRE_REPO_FOLDER, MAPPINGS_FOLDER);

            foreach (var mappingFile in Directory.EnumerateFiles(mappingsDirectoryPath))
            {
                try
                {
                    var data = JsonConvert.DeserializeObject<GenreMappingData>(
                        File.ReadAllText(mappingFile),
                        new JsonSerializerSettings() { MissingMemberHandling = MissingMemberHandling.Error }
                    );
                

                    var localizedGenre = _getLocalizedGenre(data.name);

                    // This is the subgenre-less mapping that the genre name itself, and all if its aliases, will point to
                    var genreMapping = new Mapping(localizedGenre, null);

                    // Get all the aliases for the genre and map them
                    var allMappingKeys = _getAllKeys(data.name, data.prefixes, data.suffixes, data.substitutions);
                    foreach (var key in allMappingKeys)
                    {
                        _addMapping(key, genreMapping);
                    }

                    foreach (var (subgenreName, subgenreData) in data.subgenres)
                    {
                        var localizedSubgenre = subgenreData.localizations.GetValueOrDefault(
                            Localization.LocalizationManager.CultureCode,
                            subgenreName
                        );

                        var subgenreMapping = new Mapping(localizedGenre, localizedSubgenre);

                        // Get all the aliases for the genre and map them
                        var allSubgenreMappingKeys = _getAllKeys(subgenreName, subgenreData.prefixes, subgenreData.suffixes, subgenreData.substitutions);
                        foreach (var key in allSubgenreMappingKeys)
                        {
                            _addMapping(key, subgenreMapping);
                        }
                    }
                }
                catch (Exception ex)
                {
                    YargLogger.LogException(ex, $"Failed to deserialize Genrelizer file {mappingFile}");
                }
            }
        }

        private static List<string> _getAllKeys(string original, List<string> prefixes, List<string> suffixes,
            Dictionary<string, List<string>> substitutions)
        {
            List<string> results = new();

            // First, apply all substitution combinations. This is essentially a cartesian product operation,
            // except with the possibility of selecting no value from each list.
            List<List<(string replacee, string replacer)>> substitutionSets = new() { new() };
            foreach (var substring in substitutions.Keys)
            {
                var substitutionList = substitutions[substring];

                List<List<(string replacee, string replacer)>> substitutionSetsToAdd = new();

                foreach (var substitutionSet in substitutionSets)
                {
                    foreach (var substitution in substitutionList)
                    {
                        substitutionSetsToAdd.Add(new(substitutionSet) { (substring, substitution) });
                    }
                }

                substitutionSets.AddRange(substitutionSetsToAdd);
            }

            foreach (var substitutionSet in substitutionSets)
            {
                var result = original;

                foreach (var substitution in substitutionSet)
                {
                    result = result.Replace(substitution.replacee, substitution.replacer, StringComparison.OrdinalIgnoreCase);
                }

                results.Add(result);

                // Also apply each combination of suffix and prefix to each substitution result
                foreach (var suffix in suffixes)
                {
                    results.Add(result + suffix);

                    foreach (var prefix in prefixes)
                    {
                        results.Add(prefix + result + suffix);
                    }
                }

                foreach (var prefix in prefixes)
                {
                    results.Add(prefix + result);
                }
            }

            return results;
        }



        private static async UniTask _downloadGenreMappings(LoadingContext context)
        {
            context.SetLoadingText("Downloading genre mappings...");

            // Create the sources folder if it doesn't exist
            Directory.CreateDirectory(GenresFolder);

            context.SetSubText("Checking version...");
            string genreVersionPath = System.IO.Path.Combine(GenresFolder, "version.txt");
            string currentVersion = null;
            try
            {
                if (File.Exists(genreVersionPath))
                {
                    currentVersion = await File.ReadAllTextAsync(genreVersionPath);
                }
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
                var request = UnityWebRequest.Get(GENRE_COMMIT_URL);
                request.SetRequestHeader("User-Agent", "YARG");
                request.timeout = 2;

                // Send the request and wait for the response
                await request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    // Read the JSON
                    var json = JArray.Parse(request.downloadHandler.text);
                    newestVersion = json[0]["sha"]!.ToString();
                }

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
            var repoDir = System.IO.Path.Combine(GenresFolder, GENRE_REPO_FOLDER);
            if (newestVersion == currentVersion && Directory.Exists(repoDir))
            {
                return;
            }

            // Otherwise, update!
            try
            {
                // Download
                context.SetSubText("Downloading new version...");
                string zipPath = System.IO.Path.Combine(GenresFolder, "update.zip");
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
                var ignoreFolder = System.IO.Path.Combine(repoDir, "ignore");
                if (Directory.Exists(ignoreFolder))
                {
                    Directory.Delete(ignoreFolder, true);
                }

                var githubFolder = System.IO.Path.Combine(repoDir, ".github");
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
                await File.WriteAllTextAsync(System.IO.Path.Combine(GenresFolder, "version.txt"), newestVersion);

                // Delete the zip
                File.Delete(zipPath);
            }
            catch (Exception e)
            {
                YargLogger.LogException(e, "Failed to download newest song genre version.");
            }
        }

        private static Mapping _getGenresOrDefault(string? rawGenre, string? rawSubgenre, string artist)
        {
            if (string.IsNullOrEmpty(rawGenre))
            {
                // If neither value is provided, return nothing
                if (string.IsNullOrEmpty(rawSubgenre))
                {
                    return new(
                        Localization.Localize.Key("Menu.MusicLibrary.Genre.UnknownGenre"),
                        null
                    );
                }

                // If only a subgenre is provided (not expected), treat it as the genre
                return _handleLoneGenre(rawSubgenre, artist);
            }

            if (string.IsNullOrEmpty(rawSubgenre))
            {
                return _handleLoneGenre(rawGenre, artist);
            }

            return _handleGenreSubgenrePair(rawGenre, rawSubgenre, artist);
        }

        private static Mapping _handleLoneGenre(string rawGenre, string artist) {

            // Scan up front for the Reggae/Ska special case
            if (rawGenre.ToLower() is REGGAE_SKA)
            {
                return _handleReggaeSkaSpecialCase(artist);
            }


            if (_mappings.ContainsKey(rawGenre))
            {
                return _mappings[rawGenre];
            }
            // Not recognized, but there are a couple more things we can try

            if (rawGenre.Contains('/'))
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
                 * yield results, we'll try matching the content that comes after the *last* slash. This yields
                 * "Neoclassical Metal" and "Soft Jazz", which are each mapped subgenres.
                 * 
                 * If neither of these options work out, it probably isn't worth trying stuff between slashes, so give up
                 */

                // Attempt Pattern A
                var beforeFirstSlash = rawGenre[0..rawGenre.IndexOf('/')].TrimEnd();
                if (_mappings.ContainsKey(beforeFirstSlash))
                {
                    return _mappings[beforeFirstSlash];
                }

                // Attempt Pattern B
                var afterLastSlash = rawGenre.Substring(rawGenre.LastIndexOf('/') + 1).TrimStart();
                if (_mappings.ContainsKey(afterLastSlash))
                {
                    return _mappings[afterLastSlash];
                }
            }

            if (rawGenre.Contains(','))
            {
                /* 
                 * Pattern A can also occur with commas rather than slashes, like "Hard Rock, Heavy Metal", so try
                 * that too. Pattern B generally doesn't appear with commas, so don't bother with that.
                 */
                var beforeFirstComma = rawGenre[0..rawGenre.IndexOf(',')].TrimEnd();
                if (_mappings.ContainsKey(beforeFirstComma))
                {
                    return _mappings[beforeFirstComma];
                }
            }

            // We've exhausted all of our options, so default to Other
            return new(
                Localization.Localize.Key("Menu.MusicLibrary.Genre.Other"),
                _sanitize(rawGenre)
            );
        }

        private static Mapping _handleGenreSubgenrePair(string rawGenre, string rawSubgenre, string artist)
        {
            // Scan up front for the Reggae/Ska special case
            if (rawGenre.ToLower() is REGGAE_SKA && rawSubgenre.ToLower() is "other")
            {
                return _handleReggaeSkaSpecialCase(artist);
            }


            // Check if this is a telltale value pair from Magma, for which we have a ready-to-go mapping
            if (MAGMA_MAPPINGS.TryGetValue((rawGenre, rawSubgenre), out var magmaMapping))
            {
                return _handleMagmaValuePair(magmaMapping.genre, magmaMapping.subgenre);
            }

            // Identical genre/subgenre pairs get treated as just a genre
            if (rawGenre == rawSubgenre)
            {
                return _handleLoneGenre(rawGenre, artist);
            }

            // Handle the genre first. We're going to pass it through the Genrelizer data, but we
            // only care about the returned genre here, not the subgenre. This covers several
            // possible scenarios for the raw genre value:
            //
            //  -If the provided genre is standard, then we'll get that same standard value back
            //
            //  -If the provided genre is an alias for a standard genre, or has the wrong
            //      capitalization, we'll get back an aliased and standardized version of it
            //
            //  -If the genre isn't standard at all, but happens to match a subgenre, then the
            //      genre that that subgenre falls under is our best guess anyway. We'll throw
            //      away the returned subgenre because we'd rather prioritize the provided one
            //
            //  -If it doesn't even match a subgenre, then we'll get Other back. The subgenre will
            //      get a chance to override that value later, but for now it's our fallback
            var genre = _handleLoneGenre(rawGenre, artist).Genre;


            // Now the subgenre. Pass that through the Genrelizer data too. This time we care about
            // both returned fields
            var subgenreMapping = _handleLoneGenre(rawSubgenre, artist);

            // The subgenre is a little more complicated
            string subgenre;

            // If we got a subgenre back, we want to use it. It's the standardized form of the chart-
            // provided subgenre value
            if (!string.IsNullOrEmpty(subgenreMapping.Subgenre))
            {
                subgenre = subgenreMapping.Subgenre;
            }

            // If we didn't get a subgenre back, then the provided subgenre is probably also a standard
            // genre. For example, a song might be tagged as "Heavy Metal > Metalcore", in which case the
            // "Metalcore" subgenre would have returned the "Metalcore" genre and no subgenre - in that
            // case, we want the returned genre as the standardized form of the provided subgenre...
            else
            {
                // ...UNLESS that happens to be what we already have for the genre! For example, imagine
                // a chart tagged as "Metalcore > Metal Core". That wouldn't have hit the "redundant values"
                // early-exit, but at this point we would have "Metalcore > Metalcore", so we'll scrub the
                // subgenre in that case as well.
                if (subgenreMapping.Genre == genre)
                {
                    subgenre = null;
                } else
                {
                    subgenre = subgenreMapping.Genre;
                }
            }

            // If the genre is Other at this point (even if it was originally provided that way!), then
            // we'll fall back to the genre provided by the subgenre mapping. That could still be Other,
            // of course, but we've done all we can do.
            if (genre == _getLocalizedGenre("Other"))
            {
                genre = subgenreMapping.Genre;

                // If we just created a duplicate genre/subgenre pair, then scrub the subgenre
                if (genre == subgenre)
                {
                    subgenre = null;
                }
            }

            return new(genre, subgenre);
        }

        private static Mapping _handleMagmaValuePair(string magmaGenre, string magmaSubgenre)
        {
            // Localize the returned genre directly
            var genre = Localization.Localize.Key("Menu.MusicLibrary.Genre", GENRE_LOCALIZATION_KEYS.GetValueOrDefault(magmaGenre, "Other"));

            string subgenre = null;

            // Run the subgenre through the Genrelzier data, just for the purpose of localizing it
            if (magmaSubgenre is not null)
            {
                if (_mappings.TryGetValue(magmaSubgenre, out var subgenreMapping))
                {
                    subgenre = subgenreMapping.Subgenre;
                }
                else
                {
                    // Belt-and-suspenders; we shouldn't have any unrecognized or unsanitized subgenres
                    // in the hardcoded Magma mappings
                    subgenre = _sanitize(magmaSubgenre);
                }
            }

            return new(genre, subgenre);
        }

        private static Mapping _handleReggaeSkaSpecialCase(string artist)
        {
            if (artist is "UB40" or "Zing Experience" || artist.Contains("Bob Marley"))
            {
                return new(REGGAE, null);
            }
            return new(SKA, null);
        }

        private static string _sanitize(string subgenre)
        {
            var textInfo = new CultureInfo(Localization.LocalizationManager.CultureCode).TextInfo;
            return textInfo.ToTitleCase(subgenre.Trim());
        }
    }
}
