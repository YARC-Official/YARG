using System;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEngine;
using YARG.Core.Logging;

namespace YARG.Helpers
{
    [DefaultExecutionOrder(-3000)]
    public static class PathHelper
    {
        private static readonly Regex _fileNameSanitize = new("([^a-zA-Z0-9])", RegexOptions.Compiled);

        /// <summary>
        /// Where settings, scores, etc. should be stored.
        /// </summary>
        public static string PersistentDataPath { get; private set; }

        /// <summary>
        /// The root level of the persistent data path.
        /// </summary>
        public static string RealPersistentDataPath { get; private set; }

        /// <summary>
        /// The data folder in YARG's installation.
        /// </summary>
        public static string ApplicationDataPath { get; private set; }

        /// <summary>
        /// The folder where YARG's executable lies.
        /// </summary>
        public static string ExecutablePath { get; private set; }

        /// <summary>
        /// YARG's streaming assets folder.
        /// </summary>
        public static string StreamingAssetsPath { get; private set; }

        /// <summary>
        /// The location of the song cache.
        /// </summary>
        public static string SongCachePath { get; private set; }

        /// <summary>
        /// The file to write the bad songs list to.
        /// </summary>
        public static string BadSongsPath { get; private set; }

        /// <summary>
        /// YARC Launcher path.
        /// </summary>
        public static string LauncherPath { get; private set; }

        /// <summary>
        /// YARC Launcher setlist path.
        /// </summary>
        public static string SetlistPath { get; private set; }

        /// <summary>
        /// YARC Launcher venue path.
        /// </summary>
        public static string VenuePath { get; private set; }

        public static bool PathError { get; private set; }

        /// <summary>
        /// Safe options to use when enumerating files or directories.
        /// Recurses subdirectories.
        /// </summary>
        public static EnumerationOptions SafeSearchOptions { get; } = new()
        {
            RecurseSubdirectories = true,
            ReturnSpecialDirectories = false,
            IgnoreInaccessible = true,
        };

        /// <summary>
        /// Safe options to use when enumerating files or directories.
        /// Does not recurse subdirectories.
        /// </summary>
        public static EnumerationOptions SafeSearchOptions_NoRecurse { get; } = new()
        {
            ReturnSpecialDirectories = false,
            IgnoreInaccessible = true,
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        private static void Init()
        {
            // Save this data as Application.* is main thread only (why Unity)
            RealPersistentDataPath = SanitizePath(Application.persistentDataPath);
#if UNITY_EDITOR || YARG_TEST_BUILD
            PersistentDataPath = SanitizePath(Path.Combine(Application.persistentDataPath, "dev"));
#elif YARG_NIGHTLY_BUILD
            PersistentDataPath = SanitizePath(Path.Combine(Application.persistentDataPath, "nightly"));
#else
            PersistentDataPath = SanitizePath(Path.Combine(Application.persistentDataPath, "release"));
#endif

            // Persistent Data Path override passed in from CLI
            if (!string.IsNullOrWhiteSpace(CommandLineArgs.PersistentDataPath))
            {
                try
                {
                    Directory.CreateDirectory(CommandLineArgs.PersistentDataPath);
                }
                catch (IOException e)
                {
                    // YargLogger probably isn't going to work in this case, so we'll just use Unity's logging
                    Debug.LogException(e);
                    // Set a flag that we can check in a non-static method so we can pop a dialog and exit
                    PathError = true;
                }

                PersistentDataPath = SanitizePath(CommandLineArgs.PersistentDataPath);
            }

            // Get other paths that are only allowed on the main thread
            ApplicationDataPath = SanitizePath(Application.dataPath);
            ExecutablePath = Directory.GetParent(ApplicationDataPath)?.FullName;
            StreamingAssetsPath = SanitizePath(Application.streamingAssetsPath);

            // Get song scanning paths
            SongCachePath = Path.Combine(PersistentDataPath, "songcache.bin");
            BadSongsPath = Path.Combine(PersistentDataPath, "badsongs.txt");

            // Get the launcher paths
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            // Thanks Apple
            var localAppdata = Path.Combine(Environment.GetEnvironmentVariable("HOME"),
                "Library", "Application Support");
#else
            var localAppdata = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
#endif
            LauncherPath = Path.Join(localAppdata, "YARC", "Launcher");

            // Get official setlist path
            // (this is replaced by the launch argument if it is set)
            (SetlistPath, VenuePath) = FindLauncherPaths();
        }

        private static (string, string) FindLauncherPaths()
        {
            // Use the launcher settings to find the setlist path
            string settingsPath = Path.Join(LauncherPath, "settings.json");
            if (!File.Exists(settingsPath))
            {
                YargLogger.LogWarning("Failed to find launcher settings file. Game is most likely running without the launcher.");
                return (null, null);
            }

            try
            {
                var settingsFile = File.ReadAllText(settingsPath);
                var json = JObject.Parse(settingsFile);
                if (!json.TryGetValue("download_location", out var downloadLocation)) return (null, null);

                string setlistPath = Path.Join(downloadLocation.ToString(), "Setlists");
                string venuePath = Path.Join(downloadLocation.ToString(), "Venues");
                if (!Directory.Exists(setlistPath))
                {
                    setlistPath = null;
                }

                if (!Directory.Exists(venuePath))
                {
                    venuePath = null;
                }

                return (setlistPath, venuePath);
            }
            catch (Exception e)
            {
                YargLogger.LogException(e, "Failed to load setlist path.");
                return (null, null);
            }
        }

        public static void SetPathsFromDownloadLocation(string downloadLocation)
        {
            SetlistPath = Path.Join(downloadLocation, "Setlists");
            VenuePath = Path.Join(downloadLocation, "Venues");
        }

        /// <summary>
        /// Safely enumerates a directory for files using the given processing delegate.
        /// </summary>
        /// <param name="path">
        /// The path to enumerate files from.
        /// </param>
        /// <param name="processFile">
        /// The action used to process the enumerated files. Return false to stop enumeration, true to continue.
        /// </param>
        public static void SafeEnumerateFiles(string path, bool recurse, Func<string, bool> processFile)
        {
            SafeEnumerateFiles(path, "*", recurse, processFile);
        }

        /// <summary>
        /// Safely enumerates a directory for files using the given processing delegate.
        /// </summary>
        /// <param name="path">
        /// The path to enumerate files from.
        /// </param>
        /// <param name="searchPattern">
        /// The search pattern to use in the enumeration.
        /// </param>
        /// <param name="processFile">
        /// The action used to process the enumerated files. Return false to stop enumeration, true to continue.
        /// </param>
        public static void SafeEnumerateFiles(string path, string searchPattern, bool recurse,
            Func<string, bool> processFile)
        {
            var options = recurse ? SafeSearchOptions : SafeSearchOptions_NoRecurse;
            foreach (var file in Directory.EnumerateFiles(path, searchPattern, options))
            {
                try
                {
                    if (!processFile(file)) return;
                }
                catch (Exception ex)
                {
                    YargLogger.LogException(ex, $"Error while enumerating {path}! Current file: {file}");
                }
            }
        }

        /// <summary>
        /// Safely enumerates a directory for files using the given processing delegate.
        /// </summary>
        /// <param name="path">
        /// The path to enumerate files from.
        /// </param>
        /// <param name="processDirectory">
        /// The action used to process the enumerated directories.
        /// Return false to stop enumeration, true to continue.
        /// </param>
        public static void SafeEnumerateDirectories(string path, bool recurse, Func<string, bool> processDirectory)
        {
            SafeEnumerateDirectories(path, "*", recurse, processDirectory);
        }

        /// <summary>
        /// Safely enumerates a directory for other directories using the given processing delegate.
        /// </summary>
        /// <param name="path">
        /// The path to enumerate directories from.
        /// </param>
        /// <param name="searchPattern">
        /// The search pattern to use in the enumeration.
        /// </param>
        /// <param name="processDirectory">
        /// The action used to process the enumerated directories.
        /// Return false to stop enumeration, true to continue.
        /// </param>
        public static void SafeEnumerateDirectories(string path, string searchPattern, bool recurse,
            Func<string, bool> processDirectory)
        {
            var options = recurse ? SafeSearchOptions : SafeSearchOptions_NoRecurse;
            foreach (var directory in Directory.EnumerateDirectories(path, searchPattern, options))
            {
                try
                {
                    if (!processDirectory(directory)) return;
                }
                catch (Exception ex)
                {
                    YargLogger.LogException(ex, $"Error while enumerating {path}! Current directory: {directory}");
                }
            }
        }

        public static string SanitizePath(string path)
        {
            // this is to handle a strange edge case in path naming in windows.
            // modern windows can handle / or \ in path names with seemingly one exception,
            // if there is a space in the user name then try forward slash appdata, it will break at the first space so:
            // c:\users\joe blow\appdata <- okay!
            // c:/users/joe blow\appdata <- okay!
            // c:/users/joe blow/appdata <- "Please choose an app to open joe"
            // so let's just set them all to \ on windows to be sure.
            path = path.Replace('/', Path.DirectorySeparatorChar);

            return path;
        }

        /// <summary>
        /// Converts all symbols and spaces into "_".
        /// </summary>
        public static string SanitizeFileName(string fileName)
        {
            return _fileNameSanitize.Replace(fileName, "_");
        }
    }
}