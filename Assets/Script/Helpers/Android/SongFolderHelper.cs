#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;
using YARG.Settings;

namespace YARG.Helpers
{
    /// <summary>
    /// Registers the Android song folders (called once from
    /// SettingsManager.LoadSettings). Order matters: the user-visible folder
    /// is FIRST (primary), the app-private folder LAST (fallback).
    ///
    ///   1. /storage/emulated/0/YARG/songs  (PRIMARY — visible in any file
    ///      manager; needs all-files access on Android 11+)
    ///   2. &lt;SD-card&gt;/YARG/songs            (only if a removable card exists)
    ///   3. &lt;persistentDataPath&gt;/songs      (FALLBACK — app-private, always
    ///      readable, no permission needed; the same folder run2 registered)
    ///
    /// Songs in ANY of the three folders are found. Nothing here throws:
    /// a denied permission or an unwritable folder only means that folder
    /// stays empty — the game keeps working with the others.
    /// </summary>
    public static class SongFolderHelper
    {
        private const string PromptMarkerFileName = "allFilesAccessPrompted.txt";

        /// <summary>
        /// Internal storage root, e.g. /storage/emulated/0. Derived from the
        /// app-private path by trimming at "/Android/" (the same derivation
        /// the fork used); falls back to the standard emulated path.
        /// </summary>
        public static string InternalStorageRoot
        {
            get
            {
                string persistent = PathHelper.RealPersistentDataPath;
                if (!string.IsNullOrEmpty(persistent))
                {
                    int androidIndex = persistent.IndexOf("/Android/", StringComparison.Ordinal);
                    if (androidIndex > 0)
                    {
                        return persistent.Substring(0, androidIndex);
                    }
                }
                return "/storage/emulated/0";
            }
        }

        /// <summary>Primary song folder: /storage/emulated/0/YARG/songs.</summary>
        public static string PrimarySongsFolder
        {
            get { return Path.Combine(InternalStorageRoot, "YARG", "songs"); }
        }

        /// <summary>App-private fallback (the same folder run2 registered).</summary>
        public static string FallbackSongsFolder
        {
            get { return Path.Combine(PathHelper.RealPersistentDataPath, "songs"); }
        }

        public static void RegisterSongFolders()
        {
            // 1. All-files access, asked at most once per install. The prompt
            //    opens the system settings page; the user may decline and the
            //    game still works (the fallback folder below needs no
            //    permission). If the user declines, they can grant it later
            //    manually — see PANDUAN_INSTALL_ID.md.
            try
            {
                PromptAllFilesAccessOnce();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            // 2. Primary folder first, then SD card, then fallback.
            AddFolder(PrimarySongsFolder);

            try
            {
                string sdRoot = AndroidPathHelper.GetSdCardRoot();
                if (!string.IsNullOrEmpty(sdRoot))
                {
                    AddFolder(Path.Combine(sdRoot, "YARG", "songs"));
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            AddFolder(FallbackSongsFolder);
        }

        private static void PromptAllFilesAccessOnce()
        {
            if (!StoragePermissionHelper.IsAllFilesAccessSupported())
            {
                return;
            }

            if (StoragePermissionHelper.HasAllFilesAccess())
            {
                return;
            }

            string marker = Path.Combine(PathHelper.PersistentDataPath, PromptMarkerFileName);
            if (File.Exists(marker))
            {
                return;
            }

            StoragePermissionHelper.PromptAllFilesAccess();

            try
            {
                Directory.CreateDirectory(PathHelper.PersistentDataPath);
                File.WriteAllText(marker, "1");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private static void AddFolder(string folder)
        {
            try
            {
                Directory.CreateDirectory(folder);
            }
            catch (Exception e)
            {
                // Unwritable without all-files access — the folder is still
                // registered below; YARG's scanner skips unreadable folders.
                Debug.LogWarning("[SongFolderHelper] Cannot create song folder " + folder + ": " + e.Message);
            }

            try
            {
                if (SettingsManager.Settings == null)
                {
                    return;
                }

                if (!SettingsManager.Settings.SongFolders.Contains(folder))
                {
                    SettingsManager.Settings.SongFolders.Add(folder);
                    Debug.Log("[SongFolderHelper] Song folder registered: " + folder);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}
#endif
