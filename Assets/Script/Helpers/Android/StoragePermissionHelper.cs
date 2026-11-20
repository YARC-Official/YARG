#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using UnityEngine;

namespace YARG.Helpers
{
    /// <summary>
    /// All-files access (MANAGE_EXTERNAL_STORAGE), needed to read the
    /// user-visible song folder /storage/emulated/0/YARG/songs on Android 11+.
    ///
    /// Ported from JordanSanchez06/YARG (YARG-Android branch,
    /// StoragePermissionHelper.cs), hardened: the fork called
    /// isExternalStorageManager with no guards, which throws on Android 10
    /// and below (the API does not exist there). Every call here is guarded
    /// so a missing API or a denied permission can never crash the game —
    /// callers must always work without the permission (the app-private
    /// song folder needs none).
    ///
    /// Note: this is only viable because the APK is sideloaded for personal
    /// use. A Play Store release using MANAGE_EXTERNAL_STORAGE needs a
    /// declaration and is normally rejected for a game like this.
    /// </summary>
    public static class StoragePermissionHelper
    {
        /// <summary>
        /// True on Android 11 (API 30) and newer, where all-files access exists.
        /// </summary>
        public static bool IsAllFilesAccessSupported()
        {
            try
            {
                using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                {
                    return version.GetStatic<int>("SDK_INT") >= 30;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[StoragePermissionHelper] Cannot read SDK_INT: " + e.Message);
                return false;
            }
        }

        public static bool HasAllFilesAccess()
        {
            if (!IsAllFilesAccessSupported())
            {
                return false;
            }

            try
            {
                using (var env = new AndroidJavaClass("android.os.Environment"))
                {
                    return env.CallStatic<bool>("isExternalStorageManager");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[StoragePermissionHelper] HasAllFilesAccess() failed: " + e.Message);
                return false;
            }
        }

        /// <summary>
        /// Opens the system "All files access" page for this app. Returns
        /// immediately — the user grants (or declines) there and comes back
        /// to the game. Never throws.
        /// </summary>
        public static void PromptAllFilesAccess()
        {
            if (!IsAllFilesAccessSupported())
            {
                return;
            }

            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var intent = new AndroidJavaObject(
                    "android.content.Intent",
                    "android.settings.MANAGE_APP_ALL_FILES_ACCESS_PERMISSION",
                    new AndroidJavaClass("android.net.Uri")
                        .CallStatic<AndroidJavaObject>("parse", "package:" + Application.identifier)))
                {
                    Debug.Log("[StoragePermissionHelper] Prompting for all-files access.");
                    activity.Call("startActivity", intent);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[StoragePermissionHelper] PromptAllFilesAccess() failed: " + e.Message);
            }
        }
    }
}
#endif
