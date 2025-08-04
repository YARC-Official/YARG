using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace YARG.Editor.Submodules
{
    public static class SubmoduleHelper
    {
        private const string OUTPUT_FOLDER = "Assets/Plugins/Editor/Submodule";
        private const string HASH_PATH = OUTPUT_FOLDER + "/YARG.Core.githash";
        private const string REBUILD_LOCK_PATH = OUTPUT_FOLDER + "/reload_lock~";

        public static string ProjectRoot => Path.GetDirectoryName(Application.dataPath);
        public static string SubmoduleRoot => Path.Combine(ProjectRoot, "YARG.Core");

        public static bool IsReloadLocked => EditorApplication.isPlayingOrWillChangePlaymode || File.Exists(REBUILD_LOCK_PATH);

        private static bool _quitting = false;

        static SubmoduleHelper()
        {
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;

            EditorApplication.quitting += OnEditorQuit;
        }

        private static void OnBeforeAssemblyReload()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload -= OnAfterAssemblyReload;

            if (!File.Exists(REBUILD_LOCK_PATH))
            {
                File.WriteAllText(REBUILD_LOCK_PATH, "");
            }
        }

        private static void OnAfterAssemblyReload()
        {
            if (!_quitting && File.Exists(REBUILD_LOCK_PATH))
            {
                File.Delete(REBUILD_LOCK_PATH);
            }
        }

        private static void OnEditorQuit()
        {
            _quitting = true;
            if (File.Exists(REBUILD_LOCK_PATH))
            {
                File.Delete(REBUILD_LOCK_PATH);
            }
        }

        public static bool CheckCommitHash(string progTitle, float progress)
        {
            if (IsReloadLocked)
            {
                return false;
            }

            // Check the current commit hash
            string currentHash = GetCurrentCommitHash(progTitle, progress);
            if (File.Exists(HASH_PATH) && File.ReadAllText(HASH_PATH) == currentHash)
            {
                return false;
            }

            // Store new commit hash
            if (!string.IsNullOrEmpty(currentHash))
            {
                File.WriteAllText(HASH_PATH, currentHash);
            }

            return true;
        }

        private static string GetCurrentCommitHash(string progTitle, float progress)
        {
            // Ask Git what the current YARG.Core hash is
            var output = RunCommandWithOutput(
                "git",
                "rev-parse HEAD",
                progTitle,
                "Checking Git commit hash",
                progress,
                cwd: SubmoduleRoot
            );

            // The hash is output directly
            string hash = output.ReadToEnd();
            if (string.IsNullOrEmpty(hash))
            {
                throw new Exception($"Failed to get commit hash! Command output:\n{output.ReadToEnd()}");
            }

            return hash;
        }

        public static void RunCommand(
            string command,
            string args,
            string progTitle,
            string progInfo,
            float progress,
            string cwd = null
        )
        {
            var output = RunCommandWithOutput(
                command,
                args,
                progTitle,
                progInfo,
                progress,
                cwd
            );
            output.Dispose();
        }

        public static StreamReader RunCommandWithOutput(
            string command,
            string args,
            string progTitle,
            string progInfo,
            float progress,
            string cwd = null
        )
        {
            // Run the command
            using var process = Process.Start(new ProcessStartInfo()
            {
                FileName = command,
                Arguments = args,
                UseShellExecute = false, // Must be false to redirect input/output/error
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = cwd ?? ProjectRoot
            });

            while (!process.HasExited)
            {
                if (EditorUtility.DisplayCancelableProgressBar(progTitle, progInfo, progress))
                {
                    process.Kill();
                    throw new Exception($"Command was cancelled!");
                }

                Thread.Sleep(100);
            }

            // Bail out on error
            var output = process.StandardOutput;
            string error = process.StandardError.ReadToEnd();
            if (!string.IsNullOrEmpty(error) || process.ExitCode != 0)
            {
                throw new Exception($"Error when running command! Exit code: {process.ExitCode}, command output:\n{output.ReadToEnd()}{error}");
            }

            return output;
        }
    }
}
