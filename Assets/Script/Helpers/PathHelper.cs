using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace YARG.Helpers
{
    public static class PathHelper
    {
        /// <summary>
        /// Where settings, scores, etc. should be stored.
        /// </summary>
        public static string PersistentDataPath { get; private set; }

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
        /// YARC Launcher path.
        /// </summary>
        public static string LauncherPath { get; private set; }

        /// <summary>
        /// YARC Launcher setlist path.
        /// </summary>
        public static string SetlistPath { get; private set; }

        /// <summary>
        /// Safe options to use when enumerating files or directories.
        /// </summary>
        public static EnumerationOptions SafeSearchOptions { get; } = new()
        {
            RecurseSubdirectories = true,
            ReturnSpecialDirectories = false,
            IgnoreInaccessible = true,
        };

        public static void Init()
        {
            // Save this data as Application.* is main thread only (why Unity)
            PersistentDataPath = SanitizePath(Application.persistentDataPath);
            ApplicationDataPath = SanitizePath(Application.dataPath);
            ExecutablePath = Directory.GetParent(ApplicationDataPath)?.FullName;
            StreamingAssetsPath = SanitizePath(Application.streamingAssetsPath);

            // Get the launcher path
            var localAppdata = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            LauncherPath = Path.Join(localAppdata, "YARC", "Launcher");

            // Use the launcher settings to find the setlist path
            string settingsPath = Path.Join(LauncherPath, "settings.json");
            if (File.Exists(settingsPath))
            {
                try
                {
                    var settingsFile = File.ReadAllText(settingsPath);
                    var json = JObject.Parse(settingsFile);
                    if (json.TryGetValue("download_location", out var downloadLocation))
                    {
                        SetlistPath = Path.Join(downloadLocation.ToString(), "Setlists", "official");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning("Failed to load setlist path. Is it installed?");
                    Debug.LogException(e);
                }
            }
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
        public static void SafeEnumerateFiles(string path, Func<string, bool> processFile)
        {
            SafeEnumerateFiles(path, "*", processFile);
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
        public static void SafeEnumerateFiles(string path, string searchPattern, Func<string, bool> processFile)
        {
            foreach (var file in Directory.EnumerateFiles(path, searchPattern, SafeSearchOptions))
            {
                try
                {
                    if (!processFile(file))
                        return;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error while enumerating {path}! Current file: {file}");
                    Debug.LogException(ex);
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
        public static void SafeEnumerateDirectories(string path, Func<string, bool> processDirectory)
        {
            SafeEnumerateDirectories(path, "*", processDirectory);
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
        public static void SafeEnumerateDirectories(string path, string searchPattern, Func<string, bool> processDirectory)
        {
            foreach (var directory in Directory.EnumerateDirectories(path, searchPattern, SafeSearchOptions))
            {
                try
                {
                    if (!processDirectory(directory))
                        return;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error while enumerating {path}! Current directory: {directory}");
                    Debug.LogException(ex);
                }
            }
        }

        private static string SanitizePath(string path)
        {
            // this is to handle a strange edge case in path naming in windows.
            // modern windows can handle / or \ in path names with seemingly one exception,
            // if there is a space in the user name then try forward slash appdata, it will break at the first space so:
            // c:\users\joe blow\appdata <- okay!
            // c:/users/joe blow\appdata <- okay!
            // c:/users/joe blow/appdata <- "Please choose an app to open joe"
            // so let's just set them all to \ on windows to be sure.
            return path.Replace("/", Path.DirectorySeparatorChar.ToString());
        }

        /// <summary>
        /// Checks if the path <paramref name="a"/> is equal to the path <paramref name="b"/>.<br/>
        /// Platform specific case sensitivity is taken into account.
        /// </summary>
        public static bool PathsEqual(string a, string b)
        {
#if UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX
			// Linux is case sensitive
			return Path.GetFullPath(a).Equals(Path.GetFullPath(b), StringComparison.Ordinal);

#else

            // Windows and OSX are not case sensitive
            return Path.GetFullPath(a).Equals(Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);

#endif
        }

        /// <summary>
        /// Checks if the <paramref name="subPath"/> is in the <paramref name="parentPath"/>.<br/>
        /// Platform specific case sensitivity is taken into account.
        /// </summary>
        public static bool IsSubPath(string parentPath, string subPath)
        {
            if (PathsEqual(parentPath, subPath))
            {
                return true;
            }

#if UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX
			// Linux is case sensitive
			return subPath.StartsWith(parentPath + Path.PathSeparator, StringComparison.Ordinal);

#else

            // Windows and OSX are not case sensitive
            return subPath.StartsWith(parentPath + Path.PathSeparator, StringComparison.OrdinalIgnoreCase);

#endif
        }
    }
}