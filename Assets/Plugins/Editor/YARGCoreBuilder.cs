using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Compilation;
using UnityEngine;

namespace Editor
{
    public class YARGCoreBuilder: IPreprocessBuildWithReport
    {
        private const string DLL_PATH = "Assets/Plugins/YARG.Core/YARG.Core.dll";

        // Call automatically on build
        public int callbackOrder => -10000;
        public void OnPreprocessBuild(BuildReport report)
        {
            BuildYARGCoreDLL();
        }

        [MenuItem("YARG/Rebuild YARG.Core", false)]
        public static void BuildYARGCoreDLL()
        {
            Debug.Log("Rebuilding YARG.Core...");

            // Get all of the script files
            string projectRoot = Directory.GetParent(Application.dataPath)?.ToString();
            string submodulePath = Path.Join(projectRoot, "YARG.Core", "YARG.Core");
            var paths = new List<string>();
            GetAllFiles(submodulePath, paths);

            // Create the assembly with all of the scripts
            Debug.Log($"Found {paths.Count} script files.");
            var assembly = new AssemblyBuilder(DLL_PATH, paths.ToArray())
            {
                // Exclude the (maybe) already build DLL
                excludeReferences = new[]
                {
                    DLL_PATH
                },
            };

            // Called on main thread
            assembly.buildFinished += (assemblyPath, compilerMessages) =>
            {
                var errorCount = compilerMessages.Count(m => m.type == CompilerMessageType.Error);
                var warningCount = compilerMessages.Count(m => m.type == CompilerMessageType.Warning);

                Debug.Log("Done rebuilding YARG.Core!");
                Debug.Log($"Errors: {errorCount} - Warnings: {warningCount}");

                if (errorCount == 0)
                {
                    AssetDatabase.ImportAsset(assemblyPath);
                }
            };

            // Start build of assembly
            if (!assembly.Build())
            {
                Debug.LogError("Failed to start build of the YARG.Core assembly!");
                return;
            }

            // Wait
            while (assembly.status != AssemblyBuilderStatus.Finished)
            {
                System.Threading.Thread.Sleep(10);
            }
        }

        private static void GetAllFiles(string directory, List<string> outputFiles)
        {
            // Get all files in folder
            foreach (var path in Directory.GetFiles(directory))
            {
                if (Path.GetExtension(path) == ".cs")
                {
                    outputFiles.Add(path);
                }
            }

            // Recursively call for all folders in that directory
            foreach (var path in Directory.GetDirectories(directory))
            {
                GetAllFiles(path, outputFiles);
            }
        }
    }
}