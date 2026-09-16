#nullable enable
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace YARG.Editor.YargAudio
{
    [InitializeOnLoad]
    public sealed class YargAudioAutoBuilder : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        static YargAudioAutoBuilder()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
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
            if (state != PlayModeStateChange.ExitingEditMode)
            {
                return;
            }

            if (!EnsureUpToDate(isExplicit: false))
            {
                EditorApplication.isPlaying = false;
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

                if (!YARG.Audio.BASS.Native.YargAudioBindings.Reload())
                {
                    Debug.LogError($"[YargAudio AutoBuilder] Rebuilt {pluginInfo.BinaryName}, " +
                        "but the native library failed to load. Native audio is unavailable until it loads.");
                    return false;
                }

                Debug.Log($"[YargAudio AutoBuilder] Successfully rebuilt and updated {pluginInfo.BinaryName}");
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

            var details = string.IsNullOrWhiteSpace(error) ? output : error;
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
            if (!isExplicit && File.Exists(pluginInfo.DestinationBinaryPath))
            {
                Debug.LogWarning($"[YargAudio AutoBuilder] {errorMessage}\nFalling back to existing pre-built binary.");
                return true;
            }

            Debug.LogError($"[YargAudio AutoBuilder] {errorMessage}");
            return false;
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

            var srcDir = Path.Combine(nativeDir, "src");
            if (Directory.Exists(srcDir) && HasFilesNewerThan(srcDir, destinationWriteTime))
            {
                return true;
            }

            var includeDir = Path.Combine(nativeDir, "include");
            if (Directory.Exists(includeDir) && HasFilesNewerThan(includeDir, destinationWriteTime))
            {
                return true;
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
