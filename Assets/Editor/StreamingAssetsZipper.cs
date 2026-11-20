using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Android build step: packs Assets/StreamingAssets into StreamingAssets.zip
/// (plus a StreamingAssets.version file) BEFORE Unity packages the APK, so
/// the on-device extractor (StreamingAssetsExtractor) can read them.
///
/// This is a rewrite of the fork's BuildStreamingAssestsZipper.cs
/// (JordanSanchez06/YARG), fixing its two flaws:
///   - The fork ran as a POST-build step, i.e. AFTER the APK was already
///     packaged — so the zip only entered the APK on the NEXT build. This
///     runs as a PRE-build step (IPreprocessBuildWithReport), so the zip is
///     in the same APK it was built for. A post-build step here only deletes
///     the zip artifact from the project folder afterwards.
///   - The fork had no versioning (its ExtractedVersionKey was declared but
///     never used). This writes StreamingAssets.version: the first 16 hex
///     chars of a SHA-256 over every file's relative path + contents
///     (excluding the zip and the version file themselves). The extractor
///     compares it with the extracted copy and re-extracts on mismatch.
///
/// The zip is a build artifact: add Assets/StreamingAssets/StreamingAssets.zip
/// to .gitignore (the version file is tiny and can stay).
/// </summary>
public class StreamingAssetsZipper : IPreprocessBuildWithReport, IPostprocessBuildWithReport
{
    public int callbackOrder => 0;

    public const string ZipFileName = "StreamingAssets.zip";
    public const string VersionFileName = "StreamingAssets.version";

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.Android)
        {
            return;
        }

        string streamingAssetsDir = Application.streamingAssetsPath;
        if (!Directory.Exists(streamingAssetsDir))
        {
            Debug.LogWarning("[StreamingAssetsZipper] No StreamingAssets folder found — skipping zip step.");
            return;
        }

        try
        {
            string version = ComputeVersion(streamingAssetsDir);
            File.WriteAllText(Path.Combine(streamingAssetsDir, VersionFileName), version);

            string zipPath = Path.Combine(streamingAssetsDir, ZipFileName);
            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }

            using (ZipArchive zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                foreach (string file in Directory.EnumerateFiles(streamingAssetsDir, "*", SearchOption.AllDirectories))
                {
                    if (string.Equals(Path.GetFileName(file), ZipFileName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    zip.CreateEntryFromFile(file, GetRelativePath(streamingAssetsDir, file), CompressionLevel.Optimal);
                }
            }

            Debug.Log("[StreamingAssetsZipper] Created " + ZipFileName + " (version " + version + ").");
        }
        catch (Exception e)
        {
            Debug.LogError("[StreamingAssetsZipper] Failed to zip StreamingAssets: " + e.Message);
        }
    }

    public void OnPostprocessBuild(BuildReport report)
    {
        // The zip is already inside the APK at this point; remove the build
        // artifact from the project so it is not shipped in other platforms'
        // builds. The version file stays (tiny, and useful for debugging).
        if (report.summary.platform != BuildTarget.Android)
        {
            return;
        }

        try
        {
            string zipPath = Path.Combine(Application.streamingAssetsPath, ZipFileName);
            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[StreamingAssetsZipper] Could not delete " + ZipFileName + " after build: " + e.Message);
        }
    }

    private static string ComputeVersion(string streamingAssetsDir)
    {
        using (SHA256 sha = SHA256.Create())
        {
            string[] files = Directory.GetFiles(streamingAssetsDir, "*", SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.Ordinal);
            foreach (string file in files)
            {
                string name = Path.GetFileName(file);
                if (string.Equals(name, ZipFileName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, VersionFileName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string relative = GetRelativePath(streamingAssetsDir, file);
                byte[] header = Encoding.UTF8.GetBytes(relative + "\n");
                sha.TransformBlock(header, 0, header.Length, null, 0);

                using (FileStream stream = new FileStream(file, FileMode.Open, FileAccess.Read))
                {
                    byte[] buffer = new byte[65536];
                    int read;
                    while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        sha.TransformBlock(buffer, 0, read, null, 0);
                    }
                }
            }

            sha.TransformFinalBlock(new byte[0], 0, 0);
            return BitConverter.ToString(sha.Hash).Replace("-", "").ToLowerInvariant().Substring(0, 16);
        }
    }

    private static string GetRelativePath(string baseDir, string file)
    {
        Uri baseUri = new Uri(baseDir + Path.DirectorySeparatorChar);
        Uri fileUri = new Uri(file);
        return Uri.UnescapeDataString(baseUri.MakeRelativeUri(fileUri).ToString())
            .Replace('/', Path.DirectorySeparatorChar);
    }
}
