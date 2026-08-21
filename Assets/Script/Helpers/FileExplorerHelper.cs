using System;
using SimpleFileBrowser;
using YARG.Core.Logging;
using YARG.Menu.Navigation;

using System.Diagnostics;
using System.IO;
using UnityEngine;
using Object = UnityEngine.Object;

namespace YARG.Helpers
{
    public static class FileExplorerHelper
    {
        private static FileBrowser _fileBrowser;

        public static void OpenChooseFolder(string startingDir, Action<string> callback)
        {
            if (_fileBrowser == null)
            {
                _fileBrowser = Object.FindFirstObjectByType<FileBrowser>(FindObjectsInactive.Include);
            }

            _fileBrowser.gameObject.SetActive(true);

            var inputBlocker = Navigator.Instance?.PushInputBlocker();

            try
            {
                FileBrowser.ShowLoadDialog((files) =>
                {
                    if (files is not { Length: > 0 })
                    {
                        inputBlocker?.Dispose();
                        return;
                    }

                    string path = files[0];
                    inputBlocker?.Dispose();

                    try
                    {
                        callback(path);
                    }
                    catch (Exception ex)
                    {
                        YargLogger.LogException(ex, $"Error when handling folder {path}!");
                    }
                }, () => inputBlocker?.Dispose(), FileBrowser.PickMode.Folders, false, startingDir, null,
                    "Choose Folder");
            }
            catch
            {
                inputBlocker?.Dispose();
                throw;
            }
        }

        public static void OpenChooseFile(string startingDir, string extension, Action<string> callback)
        {
            if (_fileBrowser == null)
            {
                _fileBrowser = Object.FindFirstObjectByType<FileBrowser>(FindObjectsInactive.Include);
            }

            _fileBrowser.gameObject.SetActive(true);

            if (string.IsNullOrEmpty(extension))
            {
                FileBrowser.SetFilters(true);
            }
            else
            {
                FileBrowser.SetFilters(false, $".{extension}");
            }

            var inputBlocker = Navigator.Instance?.PushInputBlocker();

            try
            {
                FileBrowser.ShowLoadDialog((files) =>
                {
                    if (files is not { Length: > 0 })
                    {
                        inputBlocker?.Dispose();
                        return;
                    }

                    string path = files[0];
                    inputBlocker?.Dispose();

                    try
                    {
                        callback(path);
                    }
                    catch (Exception ex)
                    {
                        YargLogger.LogException(ex, $"Error when handling file {path}!");
                    }
                }, () => inputBlocker?.Dispose(), FileBrowser.PickMode.Files, false, startingDir, null,
                    "Choose File");
            }
            catch
            {
                inputBlocker?.Dispose();
                throw;
            }
        }

        public static void OpenSaveFile(string startingDir, string defaultName, string extension,
            Action<string> callback)
        {
            if (_fileBrowser == null)
            {
                _fileBrowser = Object.FindFirstObjectByType<FileBrowser>(FindObjectsInactive.Include);
            }

            _fileBrowser.gameObject.SetActive(true);

            if (string.IsNullOrEmpty(extension))
            {
                FileBrowser.SetFilters(true);
            }
            else
            {
                FileBrowser.SetFilters(false, $".{extension}");
            }

            var inputBlocker = Navigator.Instance?.PushInputBlocker();

            try
            {
                FileBrowser.ShowSaveDialog((path) =>
                {
                    if (path is not { Length: > 0 })
                    {
                        inputBlocker?.Dispose();
                        return;
                    }

                    var file = path[0];

                    if (string.IsNullOrEmpty(file))
                    {
                        inputBlocker?.Dispose();
                        return;
                    }

                    inputBlocker?.Dispose();

                    try
                    {
                        callback(file);
                    }
                    catch (Exception ex)
                    {
                        YargLogger.LogException(ex, $"Error when saving file {file}!");
                    }
                }, () => inputBlocker?.Dispose(), FileBrowser.PickMode.Files, false, startingDir,
                    $"{defaultName}.{extension}", "Save File");
            }
            catch
            {
                inputBlocker?.Dispose();
                throw;
            }
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
