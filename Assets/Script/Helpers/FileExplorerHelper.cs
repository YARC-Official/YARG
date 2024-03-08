using System;
using SFB;

#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX

using System.Diagnostics;

#else

using UnityEngine;
using YARG.Menu.Persistent;

#endif

using Debug = UnityEngine.Debug;

namespace YARG.Helpers
{
    public static class FileExplorerHelper
    {
        public static void OpenChooseFolder(string startingDir, Action<string> callback)
        {
            StandaloneFileBrowser.OpenFolderPanelAsync("Choose Folder", startingDir, false, (files) =>
            {
                if (files is not { Length: > 0 })
                    return;

                string path = files[0];
                try
                {
                    callback(path);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error when handling folder {path}!");
                    Debug.LogException(ex);
                }
            });
        }

        public static void OpenChooseFile(string startingDir, string extension, Action<string> callback)
        {
            StandaloneFileBrowser.OpenFilePanelAsync("Choose File", startingDir, extension, false, (files) =>
            {
                if (files is not { Length: > 0 })
                    return;

                string path = files[0];
                try
                {
                    callback(path);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error when handling file {path}!");
                    Debug.LogException(ex);
                }
            });
        }

        public static void OpenSaveFile(string startingDir, string defaultName, string extension,
            Action<string> callback)
        {
            StandaloneFileBrowser.SaveFilePanelAsync("Save File", startingDir, defaultName, extension, (path) =>
            {
                if (string.IsNullOrEmpty(path))
                    return;

                try
                {
                    callback(path);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error when saving file {path}!");
                    Debug.LogException(ex);
                }
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
            DialogManager.Instance.ShowMessage(
                "Path Copied To Clipboard",
                "Your system does not support the opening of the file explorer dialog, so the path of the folder has " +
                "been copied to your clipboard.");
#endif
        }
    }
}