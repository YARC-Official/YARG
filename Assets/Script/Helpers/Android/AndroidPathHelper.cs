#if UNITY_ANDROID
using UnityEngine;

namespace YARG.Helpers
{
    /// <summary>
    /// Android storage paths. Ported from JordanSanchez06/YARG (YARG-Android
    /// branch, AndroidPathHelper.cs) unchanged in behaviour.
    /// </summary>
    public static class AndroidPathHelper
    {
        /// <summary>
        /// Returns the root path of the external SD card, if available
        /// (e.g. /storage/XXXX-XXXX), or null when there is no removable card.
        /// </summary>
        public static string GetSdCardRoot()
        {
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var context = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    // Get all available external files directories
                    AndroidJavaObject[] externalDirs = context.Call<AndroidJavaObject[]>("getExternalFilesDirs", (object) null);
                    if (externalDirs == null || externalDirs.Length < 2)
                    {
                        return null; // No extra SD card
                    }

                    // Usually, the second directory is the SD card
                    AndroidJavaObject sdDir = externalDirs[1];
                    if (sdDir == null)
                    {
                        return null;
                    }

                    string sdPath = sdDir.Call<string>("getAbsolutePath"); // e.g. /storage/<UUID>/Android/data/com.yarc.yarg/files

                    // Trim "/Android/data/..." to get the SD card root
                    int androidIndex = sdPath.IndexOf("/Android/");
                    if (androidIndex > 0)
                    {
                        sdPath = sdPath.Substring(0, androidIndex);
                    }

                    return sdPath;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError("[AndroidPathHelper] Failed to get SD card root: " + e.Message);
                return null;
            }
        }
    }
}
#endif
