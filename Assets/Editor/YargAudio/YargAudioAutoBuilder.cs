#nullable enable
using System;
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
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                if (!EnsureUpToDate(isExplicit: false))
                {
                    EditorApplication.isPlaying = false;
                }
            }
        }

        [MenuItem("YARG/Audio/Rebuild Native Audio (This Platform)")]
        public static void RebuildManual() =>
            EnsureUpToDate(isExplicit: true);

        [MenuItem("YARG/Audio/Rebuild Native Audio (All Platforms - Slow)")]
        public static void RebuildAllPlatformsManual() =>
            YargAudioPackageBuilder.RebuildAllPlatforms();

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

            if (!isExplicit && !nativeDir.HasNewerSourcesThan(pluginInfo.Value.DestinationBinaryPath))
            {
                return true;
            }

            return ExecuteBuild(nativeDir, pluginInfo.Value, isExplicit);
        }

        private static bool ExecuteBuild(string nativeDir, PluginInfo pluginInfo, bool isExplicit)
        {
            try
            {
                int configureExit = RunProcess("cmake", $"--preset {pluginInfo.ConfigurePreset}", nativeDir, out _, out string configError);
                if (configureExit != 0)
                {
                    Debug.LogError($"[YargAudio AutoBuilder] CMake configure failed (exit {configureExit}):\n{configError}");
                    return false;
                }

                int buildExit = RunProcess("cmake", $"--build --preset {pluginInfo.BuildPreset} --parallel", nativeDir, out string buildOutput, out string buildError);
                if (buildExit != 0)
                {
                    var errorMsg = string.IsNullOrWhiteSpace(buildError) ? buildOutput : buildError;
                    Debug.LogError($"[YargAudio AutoBuilder] CMake build failed (exit {buildExit}):\n{errorMsg}");
                    return false;
                }

                var builtPath = ResolveBuiltBinaryPath(nativeDir, pluginInfo);
                if (!File.Exists(builtPath))
                {
                    Debug.LogError($"[YargAudio AutoBuilder] Built binary not found at: {builtPath}");
                    return false;
                }

                var destDir = Path.GetDirectoryName(pluginInfo.DestinationBinaryPath);
                if (!string.IsNullOrEmpty(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }

                File.Copy(builtPath, pluginInfo.DestinationBinaryPath, overwrite: true);
                YARG.Audio.BASS.Native.YargAudioBindings.Reload();
                Debug.Log($"[YargAudio AutoBuilder] Successfully rebuilt and updated {pluginInfo.BinaryName}");
                return true;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                if (File.Exists(pluginInfo.DestinationBinaryPath))
                {
                    Debug.LogWarning("[YargAudio AutoBuilder] CMake executable not found in PATH; using existing pre-built binary.");
                    return true;
                }

                Debug.LogError("[YargAudio AutoBuilder] CMake executable not found in PATH and no pre-built binary exists. Please install CMake 3.25+.");
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[YargAudio AutoBuilder] Unexpected error during build: {ex.Message}");
                return false;
            }
        }

        private static string ResolveBuiltBinaryPath(string nativeDir, PluginInfo info) =>
            Application.platform switch
            {
                RuntimePlatform.WindowsEditor => ResolveWindowsBuiltBinaryPath(nativeDir, info.BinaryName),
                RuntimePlatform.LinuxEditor => Path.Combine(nativeDir, "build", "linux-x64", info.BinaryName),
                RuntimePlatform.OSXEditor => Path.Combine(nativeDir, "build", "macos-universal", info.BinaryName),
                _ => Path.Combine(nativeDir, "build", "windows-x64", "Release", info.BinaryName)
            };

        private static string ResolveWindowsBuiltBinaryPath(string nativeDir, string binaryName)
        {
            var releasePath = Path.Combine(nativeDir, "build", "windows-x64", "Release", binaryName);
            if (File.Exists(releasePath))
            {
                return releasePath;
            }

            var debugPath = Path.Combine(nativeDir, "build", "windows-x64", "Debug", binaryName);
            if (File.Exists(debugPath))
            {
                return debugPath;
            }

            return releasePath;
        }

        private static int RunProcess(string filename, string arguments, string workingDirectory, out string stdout, out string stderr)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = filename,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();
            stdout = process.StandardOutput.ReadToEnd();
            stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode;
        }

        private static PluginInfo? GetPlatformPluginInfo(string projectRoot) =>
            Application.platform switch
            {
                RuntimePlatform.WindowsEditor => new PluginInfo(
                    configurePreset: "windows-x64",
                    buildPreset: "windows-x64-release",
                    binaryName: "yarg_audio.dll",
                    destinationBinaryPath: Path.Combine(projectRoot, "Assets", "Plugins", "YargAudio", "Windows", "x86_64", "yarg_audio.dll")
                ),
                RuntimePlatform.LinuxEditor => new PluginInfo(
                    configurePreset: "linux-x64",
                    buildPreset: "linux-x64-release",
                    binaryName: "libyarg_audio.so",
                    destinationBinaryPath: Path.Combine(projectRoot, "Assets", "Plugins", "YargAudio", "Linux", "x86_64", "libyarg_audio.so")
                ),
                RuntimePlatform.OSXEditor => new PluginInfo(
                    configurePreset: "macos-universal",
                    buildPreset: "macos-universal-release",
                    binaryName: "libyarg_audio.dylib",
                    destinationBinaryPath: Path.Combine(projectRoot, "Assets", "Plugins", "YargAudio", "Mac", "libyarg_audio.dylib")
                ),
                _ => null
            };

        private readonly struct PluginInfo
        {
            public string ConfigurePreset { get; }
            public string BuildPreset { get; }
            public string BinaryName { get; }
            public string DestinationBinaryPath { get; }

            public PluginInfo(string configurePreset, string buildPreset, string binaryName, string destinationBinaryPath)
            {
                ConfigurePreset = configurePreset;
                BuildPreset = buildPreset;
                BinaryName = binaryName;
                DestinationBinaryPath = destinationBinaryPath;
            }
        }
    }

    internal static class YargAudioAutoBuilderExtensions
    {
        internal static bool HasNewerSourcesThan(this string nativeDir, string destinationBinaryPath)
        {
            if (!File.Exists(destinationBinaryPath))
            {
                return true;
            }

            var destinationWriteTime = File.GetLastWriteTimeUtc(destinationBinaryPath);

            var srcDir = Path.Combine(nativeDir, "src");
            if (Directory.Exists(srcDir) && srcDir.HasFilesNewerThan(destinationWriteTime))
            {
                return true;
            }

            var includeDir = Path.Combine(nativeDir, "include");
            if (Directory.Exists(includeDir) && includeDir.HasFilesNewerThan(destinationWriteTime))
            {
                return true;
            }

            var cmakeLists = Path.Combine(nativeDir, "CMakeLists.txt");
            if (File.Exists(cmakeLists) && File.GetLastWriteTimeUtc(cmakeLists) > destinationWriteTime)
            {
                return true;
            }

            var cmakePresets = Path.Combine(nativeDir, "CMakePresets.json");
            if (File.Exists(cmakePresets) && File.GetLastWriteTimeUtc(cmakePresets) > destinationWriteTime)
            {
                return true;
            }

            return false;
        }

        internal static bool HasFilesNewerThan(this string directory, DateTime referenceUtc)
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
    }
}
