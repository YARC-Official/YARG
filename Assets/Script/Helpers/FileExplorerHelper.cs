using System;
using SimpleFileBrowser;
using YARG.Core.Logging;

using System.Diagnostics;

namespace YARG.Helpers
{
    public static class FileExplorerHelper
    {
        public static void OpenChooseFolder(string startingDir, Action<string> callback)
        {
            FileBrowser.ShowLoadDialog((files) =>
            {
                if (files is not { Length: > 0 })
                {
                    return;
                }

                string path = files[0];

                try
                {
                    callback(path);
                }
                catch (Exception ex)
                {
                    YargLogger.LogException(ex, $"Error when handling folder {path}!");
                }
            }, null, FileBrowser.PickMode.Folders, false, startingDir, null, "Choose Folder");
        }

        public static void OpenChooseFile(string startingDir, string extension, Action<string> callback)
        {
            if (string.IsNullOrEmpty(extension))
            {
                FileBrowser.SetFilters(true);
            }
            else
            {
                FileBrowser.SetFilters(false, $".{extension}");
            }

            FileBrowser.ShowLoadDialog((files) =>
            {
                if (files is not { Length: > 0 })
                {
                    return;
                }

                string path = files[0];

                try
                {
                    callback(path);
                }
                catch (Exception ex)
                {
                    YargLogger.LogException(ex, $"Error when handling folder {path}!");
                }
            }, null, FileBrowser.PickMode.Files, false, startingDir, null, "Choose Folder");
        }

        public static void OpenSaveFile(string startingDir, string defaultName, string extension,
            Action<string> callback)
        {
            if (string.IsNullOrEmpty(extension))
            {
                FileBrowser.SetFilters(true);
            }
            else
            {
                FileBrowser.SetFilters(false, $".{extension}");
            }

            FileBrowser.ShowSaveDialog((path) =>
            {
                if (path is not { Length: > 0 })
                {
                    return;
                }

                var file = path[0];

                if (string.IsNullOrEmpty(file))
                {
                    return;
                }

                try
                {
                    callback(file);
                }
                catch (Exception ex)
                {
                    YargLogger.LogException(ex, $"Error when saving file {file}!");
                }
            }, null, FileBrowser.PickMode.Files, false, startingDir, $"{defaultName}.{extension}", "Save File");
        }

        public static void OpenFolder(string folderPath)
        {
#if UNITY_STANDALONE_WIN
            Process.Start("explorer.exe", folderPath);
#elif UNITY_STANDALONE_OSX
            Process.Start("open", $"\"{folderPath}\"");
#elif UNITY_STANDALONE_LINUX
            Process.Start("xdg-open", folderPath);
#else
            GUIUtility.systemCopyBuffer = folderPath;
            DialogManager.Instance.ShowMessage(
                "Path Copied To Clipboard",
                "Your system does not support the opening of the file explorer dialog, so the path of the folder has " +
                "been copied to your clipboard.");
#endif
        }

        public static void OpenToFile(string filePath)
        {
#if UNITY_STANDALONE_WIN
            Process.Start("explorer.exe", $"/select, \"{filePath}\"");
#elif UNITY_STANDALONE_OSX
            Process.Start("open", $"-R \"{filePath}\"");
#elif UNITY_STANDALONE_LINUX
            Process.Start("xdg-open", Path.GetDirectoryName(filePath));
#else
            GUIUtility.systemCopyBuffer = filePath;
            DialogManager.Instance.ShowMessage(
                "Path Copied To Clipboard",
                "Your system does not support the opening of the file explorer dialog, so the path of the folder has " +
                "been copied to your clipboard.");
#endif
        }
    }
}
