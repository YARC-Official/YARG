#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;

namespace YARG.Helpers
{
    /// <summary>
    /// Extracts the bundled StreamingAssets to persistent storage on Android.
    ///
    /// Why: on Android, Application.streamingAssetsPath points inside the APK
    /// (a compressed jar: URL) and cannot be read as files — but YARG reads
    /// languages, genres, sources, SFX, calibration music and the default
    /// venue from there with plain File I/O. The zipper (see
    /// Assets/Editor/StreamingAssetsZipper.cs) packs StreamingAssets into
    /// StreamingAssets.zip + a StreamingAssets.version file at build time;
    /// this extractor unpacks them once into
    /// &lt;persistentDataPath&gt;/StreamingAssets, and PathHelper points
    /// StreamingAssetsPath there on Android.
    ///
    /// Differences from the fork's extractor (JordanSanchez06/YARG):
    ///   - SYNCHRONOUS. The fork used a fire-and-forget async entry point at
    ///     BeforeSceneLoad, so a scene could load before extraction finished
    ///     (the likely cause of the fork's "restart once after first menu
    ///     load" note). This runs from PathHelper.Init (BeforeSplashScreen) and returns only when
    ///     extraction is done or has definitively failed.
    ///   - VERSION-CHECKED. The fork declared ExtractedVersionKey but never
    ///     used it and only checked Directory.Exists, so an updated APK never
    ///     refreshed stale files. This compares the APK's
    ///     StreamingAssets.version against the extracted copy and re-extracts
    ///     on mismatch; if the APK has no version file, an existing
    ///     extraction is kept instead of wiped.
    ///   - No UnityWebRequest. The APK is read directly with java.util.zip
    ///     (synchronous JNI calls), so no frame pumping is needed.
    ///
    /// Failure policy: extraction failure never throws out of
    /// EnsureExtracted — it logs and returns false, and the game continues
    /// (the affected readers log their own errors).
    /// </summary>
    public static class StreamingAssetsExtractor
    {
        public const string ZipEntryName = "assets/StreamingAssets.zip";
        public const string VersionEntryName = "assets/StreamingAssets.version";
        public const string VersionFileName = "StreamingAssets.version";
        public const string ExtractedFolderName = "StreamingAssets";

        /// <summary>
        /// Extracts StreamingAssets if the extracted copy is missing or its
        /// version differs from the APK's. Returns true when the extracted
        /// folder is usable (freshly extracted or already up to date).
        /// </summary>
        public static bool EnsureExtracted(string persistentDataPath)
        {
            string targetDir = Path.Combine(persistentDataPath, ExtractedFolderName);
            string versionPath = Path.Combine(targetDir, VersionFileName);

            string apkVersion = null;
            try
            {
                apkVersion = ReadApkVersion();
            }
            catch (Exception e)
            {
                Debug.LogError("[StreamingAssetsExtractor] Cannot read StreamingAssets version from APK: " + e.Message);
                return false;
            }

            if (apkVersion != null && Directory.Exists(targetDir) && File.Exists(versionPath))
            {
                string extractedVersion = null;
                try
                {
                    extractedVersion = File.ReadAllText(versionPath).Trim();
                }
                catch (Exception)
                {
                    // Unreadable version file: fall through to re-extraction.
                }

                if (string.Equals(extractedVersion, apkVersion, StringComparison.Ordinal))
                {
                    Debug.Log("[StreamingAssetsExtractor] StreamingAssets already extracted (version " + apkVersion + "), skipping.");
                    return true;
                }

                Debug.Log("[StreamingAssetsExtractor] StreamingAssets version changed (" + extractedVersion + " -> " + apkVersion + "), re-extracting.");
            }
            else if (apkVersion == null && Directory.Exists(targetDir))
            {
                // No version file in this APK (built without the zipper):
                // keep the existing extraction instead of wiping it.
                Debug.LogWarning("[StreamingAssetsExtractor] No StreamingAssets.version in APK; keeping existing extraction.");
                return true;
            }

            string tempZipPath = Path.Combine(persistentDataPath, "StreamingAssets.zip.tmp");
            try
            {
                CopyApkEntryToFile(ZipEntryName, tempZipPath);

                if (Directory.Exists(targetDir))
                {
                    Directory.Delete(targetDir, true);
                }
                Directory.CreateDirectory(targetDir);
                ZipFile.ExtractToDirectory(tempZipPath, targetDir);
                File.WriteAllText(versionPath, apkVersion ?? "unknown");
                Debug.Log("[StreamingAssetsExtractor] Extracted StreamingAssets to " + targetDir + " (version " + (apkVersion ?? "unknown") + ").");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("[StreamingAssetsExtractor] Failed to extract StreamingAssets: " + e.Message);
                try
                {
                    if (Directory.Exists(targetDir))
                    {
                        Directory.Delete(targetDir, true);
                    }
                }
                catch (Exception)
                {
                    // Best effort only; a half-extracted folder is deleted above
                    // when possible, and readers will report the missing files.
                }
                return false;
            }
            finally
            {
                try
                {
                    if (File.Exists(tempZipPath))
                    {
                        File.Delete(tempZipPath);
                    }
                }
                catch (Exception)
                {
                    // Best effort only.
                }
            }
        }

        private static string ReadApkVersion()
        {
            AndroidJavaObject zipFile = OpenApkZip();
            if (zipFile == null)
            {
                return null;
            }

            try
            {
                using (AndroidJavaObject entry = zipFile.Call<AndroidJavaObject>("getEntry", VersionEntryName))
                {
                    if (entry == null)
                    {
                        return null;
                    }

                    using (AndroidJavaObject stream = zipFile.Call<AndroidJavaObject>("getInputStream", entry))
                    {
                        if (stream == null)
                        {
                            return null;
                        }

                        try
                        {
                            return ReadAllText(stream).Trim();
                        }
                        finally
                        {
                            stream.Call("close");
                        }
                    }
                }
            }
            finally
            {
                zipFile.Call("close");
                zipFile.Dispose();
            }
        }

        private static void CopyApkEntryToFile(string entryName, string destPath)
        {
            AndroidJavaObject zipFile = OpenApkZip();
            if (zipFile == null)
            {
                throw new IOException("Cannot open APK as zip: " + Application.dataPath);
            }

            try
            {
                using (AndroidJavaObject entry = zipFile.Call<AndroidJavaObject>("getEntry", entryName))
                {
                    if (entry == null)
                    {
                        throw new FileNotFoundException("APK has no entry " + entryName + " (was the project built with StreamingAssetsZipper?)");
                    }

                    using (AndroidJavaObject stream = zipFile.Call<AndroidJavaObject>("getInputStream", entry))
                    {
                        if (stream == null)
                        {
                            throw new IOException("Cannot open APK entry " + entryName);
                        }

                        try
                        {
                            using (FileStream output = new FileStream(destPath, FileMode.Create, FileAccess.Write))
                            {
                                CopyStream(stream, output);
                            }
                        }
                        finally
                        {
                            stream.Call("close");
                        }
                    }
                }
            }
            finally
            {
                zipFile.Call("close");
                zipFile.Dispose();
            }
        }

        private static void CopyStream(AndroidJavaObject input, Stream output)
        {
            byte[] buffer = new byte[65536];
            while (true)
            {
                int read = input.Call<int>("read", buffer);
                if (read <= 0)
                {
                    break;
                }
                output.Write(buffer, 0, read);
            }
        }

        private static string ReadAllText(AndroidJavaObject input)
        {
            using (MemoryStream output = new MemoryStream())
            {
                CopyStream(input, output);
                return Encoding.UTF8.GetString(output.ToArray());
            }
        }

        private static AndroidJavaObject OpenApkZip()
        {
            try
            {
                // On Android, Application.dataPath is the APK file itself.
                return new AndroidJavaObject("java.util.zip.ZipFile", Application.dataPath);
            }
            catch (Exception e)
            {
                Debug.LogError("[StreamingAssetsExtractor] Cannot open APK as zip (" + Application.dataPath + "): " + e.Message);
                return null;
            }
        }
    }
}
#endif
