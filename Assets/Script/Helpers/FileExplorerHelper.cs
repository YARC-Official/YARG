using System;
using SFB;

#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX
using System.Diagnostics;

#else
using UnityEngine;

#endif

namespace YARG.Util
{
    public static class FileExplorerHelper
    {
        public static void OpenChooseFolder(string startingDir, Action<string> callback)
        {
            StandaloneFileBrowser.OpenFolderPanelAsync("Choose Folder", startingDir, false, i =>
            {
                if (i is not { Length: > 0 })
                {
                    return;
                }

                callback(i[0]);
            });
        }

        public static void OpenChooseFile(string startingDir, string extension, Action<string> callback)
        {
            StandaloneFileBrowser.OpenFilePanelAsync("Choose File", startingDir, extension, false, i =>
            {
                if (i is not { Length: > 0 })
                {
                    return;
                }

                callback(i[0]);
            });
        }

        public static void OpenSaveFile(string startingDir, string defaultName, string extension,
            Action<string> callback)
        {
            StandaloneFileBrowser.SaveFilePanelAsync("Save File", startingDir, defaultName, extension, i =>
            {
                if (string.IsNullOrEmpty(i))
                {
                    return;
                }

                callback(i);
            });
        }

        public static void OpenFolder(string folderPath)
        {
#if UNITY_STANDALONE_WIN

            Process.Start("explorer.exe", folderPath);

#elif UNITY_STANDALONE_OSX
			Process.Start("open", $"\"{folderPath}\"");

#else
			GUIUtility.systemCopyBuffer = folderPath;

#endif
        }
    }
}