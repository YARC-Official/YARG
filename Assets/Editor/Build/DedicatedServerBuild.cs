using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Editor.Build
{
    public static class DedicatedServerBuild
    {
        private const string OutputRoot = "Build/DedicatedServer";
        private const string ExecutableName = "YARGServer";

        public static void BuildLinuxServer()
        {
            var scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                throw new BuildFailedException("No scenes are enabled for build.");
            }

            if (Directory.Exists(OutputRoot))
            {
                Directory.Delete(OutputRoot, true);
            }

            Directory.CreateDirectory(OutputRoot);

            string executablePath = Path.Combine(OutputRoot, ExecutableName + ".x86_64");

            var previousSubtarget = EditorUserBuildSettings.standaloneBuildSubtarget;
            EditorUserBuildSettings.standaloneBuildSubtarget = StandaloneBuildSubtarget.Server;

            try
            {
                var buildOptions = new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = executablePath,
                    target = BuildTarget.StandaloneLinux64,
                    targetGroup = BuildTargetGroup.Standalone,
                    options = BuildOptions.EnableHeadlessMode
                };

                var report = BuildPipeline.BuildPlayer(buildOptions);
                if (report.summary.result != BuildResult.Succeeded)
                {
                    throw new BuildFailedException($"Dedicated server build failed with result {report.summary.result}.");
                }

                string renamedExecutable = Path.Combine(OutputRoot, ExecutableName);
                if (File.Exists(renamedExecutable))
                {
                    File.Delete(renamedExecutable);
                }

                if (File.Exists(executablePath))
                {
                    File.Move(executablePath, renamedExecutable);
                }

                Debug.Log($"[DedicatedServerBuild] Build completed at {OutputRoot}.");
            }
            finally
            {
                EditorUserBuildSettings.standaloneBuildSubtarget = previousSubtarget;
            }
        }
    }
}
