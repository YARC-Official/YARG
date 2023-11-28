using System;
using System.Linq;
using UnityEditor;

namespace Editor
{
    public static class MakeTestBuild
    {
        private const string YARG_TEST_BUILD = "YARG_TEST_BUILD";

        [MenuItem("File/Make Test Build", false, 220)]
        public static void MakeTestBuildClicked()
        {
            // Get build settings
            var buildSettings = BuildPlayerWindow.DefaultBuildMethods.GetBuildPlayerOptions(default);

            // Get current defines
            // TODO: BuildTargetGroup is slated for deprecation, figure out how to do this with NamedBuildTarget instead
            var buildGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
            PlayerSettings.GetScriptingDefineSymbolsForGroup(buildGroup, out var originalDefines);
            originalDefines ??= Array.Empty<string>();

            // Set test build define
            var buildDefines = buildSettings.extraScriptingDefines ?? Array.Empty<string>();
            if (!originalDefines.Contains(YARG_TEST_BUILD) && !buildDefines.Contains(YARG_TEST_BUILD))
                ArrayUtility.Add(ref buildDefines, YARG_TEST_BUILD);
            buildSettings.extraScriptingDefines = buildDefines;

            // Build the player
            BuildPipeline.BuildPlayer(buildSettings);
        }
    }
}