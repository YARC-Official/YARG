using System;
using System.IO;
using UnityEngine;

namespace YARG.Util {
	public static class PathHelper {
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
		/// YARG's setlist path.
		/// </summary>
		public static string SetlistPath { get; private set; }

		public static void Init() {
			// Save this data as Application.* is main thread only (why Unity)
			PersistentDataPath = SanitizePath(Application.persistentDataPath);
			ApplicationDataPath = SanitizePath(Application.dataPath);
			ExecutablePath = Directory.GetParent(ApplicationDataPath)?.FullName;
			StreamingAssetsPath = SanitizePath(Application.streamingAssetsPath);

			// Get the setlist path
			var localAppdata = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			SetlistPath = Path.Join(localAppdata, "YARC", "Setlists");
		}

		private static string SanitizePath(string path) {
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
		public static bool PathsEqual(string a, string b) {
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
		public static bool IsSubPath(string parentPath, string subPath) {
			if (PathsEqual(parentPath, subPath)) {
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