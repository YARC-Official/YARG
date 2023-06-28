using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using YARG.Song;
using YARG.Util;

namespace Editor
{
    public class SourceIconsBuilder : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            var sourcesFolder = Path.Combine(Application.streamingAssetsPath, "sources");

            // Otherwise, update!
            try
            {
                // Delete version.txt
                var txt = Path.Combine(sourcesFolder, "version.txt");
                if (File.Exists(txt))
                {
                    File.Delete(txt);
                }

                // Download
                string zipPath = Path.Combine(sourcesFolder, "update.zip");
                using (var client = new WebClient())
                {
                    client.DownloadFile(SongSources.SOURCE_ZIP_URL, zipPath);
                }

                // Delete the old folder
                var repoDir = Path.Combine(sourcesFolder, SongSources.SOURCE_REPO_FOLDER);
                if (Directory.Exists(repoDir))
                {
                    Directory.Delete(repoDir, true);
                }

                // Extract the base and extras folder
                ZipFile.ExtractToDirectory(zipPath, sourcesFolder);

                // Delete the random folders
                foreach (var folder in Directory.GetDirectories(repoDir))
                {
                    if (PathHelper.PathsEqual(Path.GetFileName(folder), "base"))
                    {
                        continue;
                    }

                    Directory.Delete(folder, true);
                }

                // Delete the random files
                foreach (var file in Directory.GetFiles(repoDir))
                {
                    File.Delete(file);
                }

                // Delete the zip
                File.Delete(zipPath);
            }
            catch (Exception e)
            {
                Debug.LogError("Failed to download newest song source version.");
                Debug.LogException(e);
            }
        }
    }
}