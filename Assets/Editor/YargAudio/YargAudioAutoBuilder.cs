#nullable enable
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using YARG.Audio.BASS.Native;
using Debug = UnityEngine.Debug;

namespace YARG.Editor.YargAudio
{
    [InitializeOnLoad]
    public sealed class YargAudioAutoBuilder : IPreprocessBuildWithReport
    {
        private const string NATIVE_RELOAD_STATE = "YargAudio.ReloadState";
        private const string NATIVE_BUILD_FAILURE = "YargAudio.BuildFailure";
        private const int MAX_ERROR_LINES = 6;
        private const int MAX_ERROR_LINE_LENGTH = 1000;

        private enum ReloadState
        {
            None,
            Reload,
            EnterPlay
        }

        public int callbackOrder => 0;

        static YargAudioAutoBuilder()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            var state = (ReloadState) SessionState.GetInt(NATIVE_RELOAD_STATE, 0);
            SessionState.EraseInt(NATIVE_RELOAD_STATE);
            if (state == ReloadState.EnterPlay)
            {
                EditorApplication.delayCall += EditorApplication.EnterPlaymode;
            }
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            if (!EnsureUpToDate(isExplicit: false))
            {
                throw new BuildFailedException("[YargAudio AutoBuilder] Native audio build failed. See console for details.");
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                var failure = SessionState.GetString(NATIVE_BUILD_FAILURE, string.Empty);
                SessionState.EraseString(NATIVE_BUILD_FAILURE);
                if (failure.Length > 0)
                {
                    Debug.LogError(failure);
                }
                return;
            }

            if (state == PlayModeStateChange.EnteredEditMode)
            {
                if (YargAudioBindings.HasPendingUpdate)
                {
                    RequestReload(enterPlay: false);
                }
                return;
            }

            if (state != PlayModeStateChange.ExitingEditMode)
            {
                return;
            }

            if (!EnsureUpToDate(isExplicit: false))
            {
                EditorApplication.isPlaying = false;
                return;
            }

            bool reloadDomain = !EditorSettings.enterPlayModeOptionsEnabled ||
                (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableDomainReload) == 0;
            if (!reloadDomain && YargAudioBindings.HasPendingUpdate)
            {
                RequestReload(enterPlay: true);
                EditorApplication.isPlaying = false;
            }
        }

        private static void RequestReload(bool enterPlay)
        {
            var state = (ReloadState) SessionState.GetInt(NATIVE_RELOAD_STATE, 0);
            if (enterPlay)
            {
                SessionState.SetInt(NATIVE_RELOAD_STATE, (int) ReloadState.EnterPlay);
            }
            else if (state == ReloadState.None)
            {
                SessionState.SetInt(NATIVE_RELOAD_STATE, (int) ReloadState.Reload);
            }

            if (state == ReloadState.None)
            {
                EditorUtility.RequestScriptReload();
            }
        }

        [MenuItem("YARG/Audio/Rebuild Native Audio (This Platform)")]
        public static void RebuildManual() =>
            EnsureUpToDate(isExplicit: true);

        public static bool EnsureUpToDate(bool isExplicit = false)
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var nativeDir = Path.Combine(projectRoot, "Native", "YargAudio");
            if (!Directory.Exists(nativeDir))
            {
                return true;
            }

            var pluginInfo = GetPlatformPluginInfo(projectRoot);
            if (pluginInfo == null)
            {
                return true;
            }

            if (!isExplicit && !HasNewerSourcesThan(nativeDir, pluginInfo.Value.DestinationBinaryPath))
            {
                return true;
            }

            return ExecuteBuild(nativeDir, pluginInfo.Value, isExplicit);
        }

        private static bool ExecuteBuild(string nativeDir, PluginInfo pluginInfo, bool isExplicit)
        {
            try
            {
                var buildDir = Path.Combine(nativeDir, "build", pluginInfo.ConfigurePreset);
                var outcome = TryRunBuild(nativeDir, pluginInfo, out string builtPath, out string buildError);
                if (outcome == BuildOutcome.BuildFailed && Directory.Exists(buildDir))
                {
                    CleanBuildDirectory(buildDir);
                    outcome = TryRunBuild(nativeDir, pluginInfo, out builtPath, out buildError);
                }

                if (outcome != BuildOutcome.Success)
                {
                    return HandleBuildFailure(buildError, pluginInfo, isExplicit);
                }

                Directory.CreateDirectory(pluginInfo.DestinationDirectory);
                File.Copy(builtPath, pluginInfo.DestinationBinaryPath, overwrite: true);
                SessionState.EraseString(NATIVE_BUILD_FAILURE);

                Debug.Log($"[YargAudio AutoBuilder] Successfully rebuilt and updated {pluginInfo.BinaryName}. " +
                    "The current Play session keeps its loaded version; the next Play session uses the update.");
                return true;
            }
            catch (Exception ex)
            {
                return HandleBuildFailure($"Unexpected error during build: {ex.Message}", pluginInfo, isExplicit);
            }
        }

        private static BuildOutcome TryRunBuild(string nativeDir, PluginInfo pluginInfo, out string builtPath,
            out string errorMessage)
        {
            builtPath = string.Empty;

            if (!TryRunCmake(nativeDir, $"--preset {pluginInfo.ConfigurePreset}", "configure", out errorMessage))
            {
                return BuildOutcome.ConfigureFailed;
            }

            if (!TryRunCmake(nativeDir, $"--build --preset {pluginInfo.BuildPreset} --parallel", "build", out errorMessage))
            {
                return BuildOutcome.BuildFailed;
            }

            builtPath = ResolveBuiltBinaryPath(nativeDir, pluginInfo);
            if (!File.Exists(builtPath))
            {
                errorMessage = $"Built binary not found at: {builtPath}";
                return BuildOutcome.BuildFailed;
            }

            errorMessage = string.Empty;
            return BuildOutcome.Success;
        }

        private static bool TryRunCmake(string nativeDir, string arguments, string step, out string errorMessage)
        {
            int exitCode;
            string output;
            string error;
            try
            {
                exitCode = RunCmake(arguments, nativeDir, out output, out error);
            }
            catch (Win32Exception)
            {
                errorMessage = "CMake executable not found in PATH; please install CMake 3.25+.";
                return false;
            }

            if (exitCode == 0)
            {
                errorMessage = string.Empty;
                return true;
            }

            var details = error.Trim() + "\n" + output.Trim();
            errorMessage = $"CMake {step} failed (exit {exitCode}):\n{details.Trim()}";
            return false;
        }

        private static void CleanBuildDirectory(string buildDir)
        {
            try
            {
                Directory.Delete(buildDir, recursive: true);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[YargAudio AutoBuilder] Failed to delete stale build directory '{buildDir}': {ex.Message}");
            }
        }

        private static bool HandleBuildFailure(string errorMessage, PluginInfo pluginInfo, bool isExplicit)
        {
            var logPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Library", "YargAudioBuild.log"));
            File.WriteAllText(logPath, errorMessage);
            var summary = SummarizeBuildFailure(errorMessage) + $"\nFull build output: {logPath}";
            bool useExistingBinary = !isExplicit && File.Exists(pluginInfo.DestinationBinaryPath);
            if (useExistingBinary)
            {
                summary += "\nPlay will use the existing binary. The latest native changes are unavailable.";
                SessionState.SetString(NATIVE_BUILD_FAILURE, summary);
            }

            Debug.LogError(summary);
            if (!Application.isBatchMode &&
                EditorUtility.DisplayDialog("Native audio rebuild failed", summary, "Open Build Log", "OK"))
            {
                EditorUtility.OpenWithDefaultApp(logPath);
            }
            return useExistingBinary;
        }

        private static string SummarizeBuildFailure(string errorMessage)
        {
            using var reader = new StringReader(errorMessage);
            var summary = new StringBuilder($"[YargAudio AutoBuilder] {reader.ReadLine()}");
            int errorCount = 0;
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.IndexOf("error:", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    line.IndexOf(": error ", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    line.IndexOf("error LNK", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    line.IndexOf("undefined reference", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    line.IndexOf("undefined symbols", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    line.IndexOf("CMake Error", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    summary.AppendLine();
                    summary.Append(line, 0, Math.Min(line.Length, MAX_ERROR_LINE_LENGTH));
                    if (++errorCount == MAX_ERROR_LINES)
                    {
                        break;
                    }
                }
            }
            return summary.ToString();
        }

        private static string ResolveBuiltBinaryPath(string nativeDir, PluginInfo info)
        {
            var presetDir = Path.Combine(nativeDir, "build", info.ConfigurePreset);
            if (Application.platform != RuntimePlatform.WindowsEditor)
            {
                return Path.Combine(presetDir, info.BinaryName);
            }

            var releasePath = Path.Combine(presetDir, "Release", info.BinaryName);
            var debugPath = Path.Combine(presetDir, "Debug", info.BinaryName);
            if (!File.Exists(releasePath) && File.Exists(debugPath))
            {
                return debugPath;
            }

            return releasePath;
        }

        private static int RunCmake(string arguments, string workingDirectory, out string stdout, out string stderr)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "cmake",
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var stdoutRead = process.StandardOutput.ReadToEndAsync();
            var stderrRead = process.StandardError.ReadToEndAsync();
            process.WaitForExit();

            stdout = stdoutRead.GetAwaiter().GetResult();
            stderr = stderrRead.GetAwaiter().GetResult();
            return process.ExitCode;
        }

        private static PluginInfo? GetPlatformPluginInfo(string projectRoot) =>
            Application.platform switch
            {
                RuntimePlatform.WindowsEditor => new PluginInfo(
                    configurePreset: "windows-x64",
                    buildPreset: "windows-x64-release",
                    binaryName: "yarg_audio.dll",
                    destinationDirectory: Path.Combine(projectRoot, "Assets", "Plugins", "YargAudio", "Windows", "x86_64")
                ),
                RuntimePlatform.LinuxEditor => new PluginInfo(
                    configurePreset: "linux-x64",
                    buildPreset: "linux-x64-release",
                    binaryName: "libyarg_audio.so",
                    destinationDirectory: Path.Combine(projectRoot, "Assets", "Plugins", "YargAudio", "Linux", "x86_64")
                ),
                RuntimePlatform.OSXEditor => new PluginInfo(
                    configurePreset: "macos-universal",
                    buildPreset: "macos-universal-release",
                    binaryName: "libyarg_audio.dylib",
                    destinationDirectory: Path.Combine(projectRoot, "Assets", "Plugins", "YargAudio", "Mac")
                ),
                _ => null
            };

        private static bool HasNewerSourcesThan(string nativeDir, string destinationBinaryPath)
        {
            if (!File.Exists(destinationBinaryPath))
            {
                return true;
            }

            var destinationWriteTime = File.GetLastWriteTimeUtc(destinationBinaryPath);

            foreach (var dir in new[] { "src", "include", "third_party" })
            {
                var path = Path.Combine(nativeDir, dir);
                if (Directory.Exists(path) && HasFilesNewerThan(path, destinationWriteTime))
                {
                    return true;
                }
            }

            var cmakeLists = Path.Combine(nativeDir, "CMakeLists.txt");
            if (File.Exists(cmakeLists) && File.GetLastWriteTimeUtc(cmakeLists) > destinationWriteTime)
            {
                return true;
            }

            var cmakePresets = Path.Combine(nativeDir, "CMakePresets.json");
            return File.Exists(cmakePresets) && File.GetLastWriteTimeUtc(cmakePresets) > destinationWriteTime;
        }

        private static bool HasFilesNewerThan(string directory, DateTime referenceUtc)
        {
            foreach (var filePath in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            {
                if (File.GetLastWriteTimeUtc(filePath) > referenceUtc)
                {
                    return true;
                }
            }

            return false;
        }

        private enum BuildOutcome
        {
            Success,
            ConfigureFailed,
            BuildFailed,
        }

        private readonly struct PluginInfo
        {
            public string ConfigurePreset { get; }
            public string BuildPreset { get; }
            public string BinaryName { get; }
            public string DestinationDirectory { get; }

            public string DestinationBinaryPath => Path.Combine(DestinationDirectory, BinaryName);

            public PluginInfo(string configurePreset, string buildPreset, string binaryName, string destinationDirectory)
            {
                ConfigurePreset = configurePreset;
                BuildPreset = buildPreset;
                BinaryName = binaryName;
                DestinationDirectory = destinationDirectory;
            }
        }
    }
}
