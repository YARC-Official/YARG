#if UNITY_IOS && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using AOT;
using Newtonsoft.Json;
using YARG.Core.Logging;

namespace YARG.Helpers
{
    /// <summary>
    ///     Native iOS folder picking (UIDocumentPickerViewController), so song
    ///     folders can live anywhere the Files app reaches: iCloud Drive, SMB
    ///     network shares, USB drives. Folders outside the app sandbox are only
    ///     accessible through security-scoped bookmarks, which are persisted
    ///     here and restored at startup before the song scan runs.
    /// </summary>
    public static class IOSFolderPicker
    {
        private delegate void FolderPickedCallback(string path, string bookmarkBase64);

        [DllImport("__Internal", EntryPoint = "yarg_pick_folder")]
        private static extern void PickFolderNative(FolderPickedCallback callback);

        [DllImport("__Internal", EntryPoint = "yarg_resolve_folder_bookmark")]
        private static extern string ResolveBookmarkNative(string bookmarkBase64);

        // Keeps the reverse-P/Invoke delegate alive while the picker is open
        private static readonly FolderPickedCallback _nativeCallback = OnFolderPicked;

        private static Action<string> _pendingCallback;

        private static string BookmarkStorePath =>
            Path.Combine(PathHelper.PersistentDataPath, "folder-bookmarks.json");

        public static void PickFolder(Action<string> callback)
        {
            if (_pendingCallback != null)
            {
                YargLogger.LogWarning("A folder picker is already open");
                return;
            }

            _pendingCallback = callback;
            PickFolderNative(_nativeCallback);
        }

        [MonoPInvokeCallback(typeof(FolderPickedCallback))]
        private static void OnFolderPicked(string path, string bookmarkBase64)
        {
            var callback = _pendingCallback;
            _pendingCallback = null;

            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            if (!string.IsNullOrEmpty(bookmarkBase64))
            {
                var bookmarks = LoadBookmarks();
                bookmarks[path] = bookmarkBase64;
                SaveBookmarks(bookmarks);
            }

            try
            {
                callback?.Invoke(path);
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, $"Error when handling folder {path}!");
            }
        }

        /// <summary>
        ///     Re-establishes security-scoped access to every bookmarked folder.
        ///     Returns a map of stored path → current path for folders whose
        ///     location changed (e.g. after an app update or a re-mounted share).
        /// </summary>
        public static Dictionary<string, string> RestoreBookmarkedFolders()
        {
            var remapped = new Dictionary<string, string>();
            var bookmarks = LoadBookmarks();
            if (bookmarks.Count == 0)
            {
                return remapped;
            }

            bool storeDirty = false;
            foreach (var (storedPath, bookmark) in new Dictionary<string, string>(bookmarks))
            {
                string currentPath;
                try
                {
                    currentPath = ResolveBookmarkNative(bookmark);
                }
                catch (Exception ex)
                {
                    YargLogger.LogException(ex, $"Failed to restore folder bookmark for {storedPath}");
                    continue;
                }

                if (string.IsNullOrEmpty(currentPath))
                {
                    YargLogger.LogFormatWarning("Folder bookmark for {0} no longer resolves", storedPath);
                    continue;
                }

                if (currentPath != storedPath)
                {
                    remapped[storedPath] = currentPath;
                    bookmarks.Remove(storedPath);
                    bookmarks[currentPath] = bookmark;
                    storeDirty = true;
                }
            }

            if (storeDirty)
            {
                SaveBookmarks(bookmarks);
            }

            return remapped;
        }

        private static Dictionary<string, string> LoadBookmarks()
        {
            try
            {
                if (File.Exists(BookmarkStorePath))
                {
                    return JsonConvert.DeserializeObject<Dictionary<string, string>>(
                        File.ReadAllText(BookmarkStorePath)) ?? new Dictionary<string, string>();
                }
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, "Failed to load folder bookmarks");
            }

            return new Dictionary<string, string>();
        }

        private static void SaveBookmarks(Dictionary<string, string> bookmarks)
        {
            try
            {
                File.WriteAllText(BookmarkStorePath, JsonConvert.SerializeObject(bookmarks));
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, "Failed to save folder bookmarks");
            }
        }
    }
}
#endif
