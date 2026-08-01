#nullable enable
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Editor.Build
{
    public static class NativeGainRuntimeSmokeBuild
    {
        private const string TEMP_SCENE = "Assets/NativeGainRuntimeSmoke.unity";

        public static void Build()
        {
            string backendName = RequireArgument("-nativeGainSmokeBackend");
            string outputPath = Path.GetFullPath(RequireArgument("-nativeGainSmokeOutput"));
            string? architecture = GetArgument("-nativeGainSmokeArchitecture");
            ScriptingImplementation backend = ParseBackend(backendName);
            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            NamedBuildTarget namedTarget = NamedBuildTarget.FromBuildTargetGroup(
                BuildPipeline.GetBuildTargetGroup(target));
            ScriptingImplementation originalBackend = PlayerSettings.GetScriptingBackend(namedTarget);
            string platformName = BuildPipeline.GetBuildTargetName(target);
            string originalArchitecture = EditorUserBuildSettings.GetPlatformSettings(
                platformName, "Architecture");

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), TEMP_SCENE))
            {
                throw new InvalidOperationException($"Failed to create temporary scene {TEMP_SCENE}.");
            }

            try
            {
                PlayerSettings.SetScriptingBackend(namedTarget, backend);
                if (!string.IsNullOrEmpty(architecture))
                {
                    EditorUserBuildSettings.SetPlatformSettings(platformName, "Architecture",
                        architecture.ToLowerInvariant());
                }

                var options = new BuildPlayerOptions
                {
                    scenes = new[] { TEMP_SCENE },
                    locationPathName = outputPath,
                    target = target,
                    targetGroup = BuildPipeline.GetBuildTargetGroup(target),
                    options = BuildOptions.Development,
                    extraScriptingDefines = new[] { "YARG_NATIVE_GAIN_SMOKE" },
                };

                BuildReport report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != BuildResult.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"Native Gain smoke build failed: {report.summary.result}, " +
                        $"errors={report.summary.totalErrors}.");
                }
            }
            finally
            {
                PlayerSettings.SetScriptingBackend(namedTarget, originalBackend);
                if (!string.IsNullOrEmpty(architecture))
                {
                    EditorUserBuildSettings.SetPlatformSettings(platformName, "Architecture",
                        originalArchitecture);
                }
                AssetDatabase.DeleteAsset(TEMP_SCENE);
            }
        }

        private static ScriptingImplementation ParseBackend(string value)
        {
            return value.ToLowerInvariant() switch
            {
                "mono" or "mono2x" => ScriptingImplementation.Mono2x,
                "il2cpp" => ScriptingImplementation.IL2CPP,
                _ => throw new ArgumentException($"Unknown scripting backend '{value}'."),
            };
        }

        private static string RequireArgument(string name)
        {
            return GetArgument(name) ??
                throw new ArgumentException($"Missing required command-line argument {name}.");
        }

        private static string? GetArgument(string name)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int i = 0; i < arguments.Length - 1; i++)
            {
                if (string.Equals(arguments[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    return arguments[i + 1];
                }
            }
            return null;
        }
    }
}
