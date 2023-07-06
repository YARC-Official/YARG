using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml;
using NugetForUnity;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Compilation;
using UnityEngine;

using Debug = UnityEngine.Debug;

namespace Editor
{
    [InitializeOnLoad]
    public class YARGCoreBuilder : IPreprocessBuildWithReport
    {
        private const string DLL_PATH = "Assets/Plugins/YARG.Core/YARG.Core.dll";
        private const string HASH_PATH = "Assets/Plugins/YARG.Core/YARG.Core.hash";

        // For automatically building in the Editor upon any recompilations
        static YARGCoreBuilder()
        {
            // Don't do anything if entering play mode
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            BuildYARGCoreDLL(wait: true);
        }

        // For automatically building upon creating a Player build
        public int callbackOrder => -10000;
        public void OnPreprocessBuild(BuildReport report)
        {
            BuildYARGCoreDLL(wait: true, force: true);
        }

        [MenuItem("YARG/Rebuild YARG.Core", false)]
        public static void BuildButton() => BuildYARGCoreDLL(wait: false, force: true);

        public static void BuildYARGCoreDLL(bool wait = false, bool force = false)
        {
            // Check the current commit hash
            string currentHash = GetCurrentCommitHash();
            if (!force && File.Exists(HASH_PATH) && File.ReadAllText(HASH_PATH) == currentHash)
                return;

            // Store new commit hash
            File.WriteAllText(HASH_PATH, currentHash);

            Debug.Log("Rebuilding YARG.Core...");

            // Get all of the script files
            if (wait)
                EditorUtility.DisplayProgressBar("Building YARG.Core", "Finding files", 0f);

            string projectRoot = Directory.GetParent(Application.dataPath)?.ToString();
            string submodulePath = Path.Join(projectRoot, "YARG.Core", "YARG.Core");
            var paths = new List<string>();
            GetAllFiles(submodulePath, paths);
            Debug.Log($"Found {paths.Count} script files.");

            // Create the assembly with all of the scripts
            if (wait)
                EditorUtility.DisplayProgressBar("Building YARG.Core", "Starting build", 0.1f);

            var assembly = new AssemblyBuilder(DLL_PATH, paths.ToArray())
            {
                // Exclude the (maybe) already build DLL
                excludeReferences = new[]
                {
                    DLL_PATH
                },
            };

            // Ensure all package references are resolved
            if (wait)
                EditorUtility.DisplayProgressBar("Building YARG.Core", "Resolving packages", 0f);

            string projectPath = Path.Join(submodulePath, "YARG.Core.csproj");
            var newReferences = FindMissingReferences(projectPath, assembly.defaultReferences);
            if (newReferences.Length > 0)
                assembly.additionalReferences = newReferences;

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
            if (wait)
                EditorUtility.DisplayProgressBar("Building YARG.Core", "Building", 0.2f);

            while (wait && assembly.status != AssemblyBuilderStatus.Finished)
            {
                System.Threading.Thread.Sleep(10);
            }

            if (wait)
                EditorUtility.ClearProgressBar();
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

        private static string[] FindMissingReferences(string projectFilePath, string[] existingReferences)
        {
            var newReferences = new List<string>();

            // Load project file
            var projectFile = new XmlDocument();
            projectFile.Load(projectFilePath);

            // Load Nuget config file, NugetForUnity is strange with this lol
            if (NugetHelper.NugetConfigFile is null)
                NugetHelper.LoadNugetConfigFile();

            // Find unresolved package references
            var packageReferences = projectFile.GetElementsByTagName("PackageReference");
            for (int index = 0; index < packageReferences.Count; index++)
            {
                // Get package info
                var packageReference = packageReferences[index];
                string packageName = packageReference.Attributes["Include"].Value;
                string packageVersion = packageReference.Attributes["Version"].Value;

                // Check for an existing reference
                if (existingReferences.Any((reference) => reference.EndsWith($"{packageName}.dll")))
                    continue;

                // Search for the package on NuGet
                var packageIdentifier = new NugetPackageIdentifier(packageName, packageVersion);
                foreach (var package in NugetHelper.Search(packageName))
                {
                    if (package.Title != packageName || !package.InRange(packageIdentifier))
                        continue;

                    // Install the package
                    Debug.Log($"Installing {package.Title} v{package.Version}");
                    if (!NugetHelper.Install(package))
                    {
                        Debug.LogWarning($"Failed to install {package.Title} v{package.Version}!");
                        continue;
                    }

                    // Get the best-fit framework
                    var frameworkGroup = NugetHelper.GetNullableBestDependencyFrameworkGroupForCurrentSettings(package);
                    if (frameworkGroup is null)
                    {
                        Debug.LogWarning($"Could not determine best framework for {package.Title} v{package.Version}! A second rebuild may be necessary for all packages to be resolved.");
                        break;
                    }

                    // Add reference to the assembly
                    string referencePath = Path.Join(NugetHelper.NugetConfigFile.RepositoryPath, frameworkGroup.TargetFramework);
                    newReferences.Add(referencePath);
                    break;
                }
            }

            return newReferences.ToArray();
        }

        private static string GetCurrentCommitHash()
        {
            // Ask Git what the current hash is for each submodule
            // (no way to target just a specific submodule, as far as I can tell)
            var process = Process.Start(new ProcessStartInfo()
            {
                FileName = "git.exe",
                Arguments = @"submodule foreach ""git rev-parse HEAD""",
                WorkingDirectory = "./",
                UseShellExecute = false, // Must be false to redirect input/output/error
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            });
            process.WaitForExit();

            // Bail out on error
            var stdOut = process.StandardOutput;
            string error = process.StandardError.ReadToEnd();
            if (!string.IsNullOrEmpty(error))
                throw new Exception($"Failed to get commit hash! Command output:\n{stdOut.ReadToEnd()}{error}");

            // Find the line that has the commit hash
            // The output is formatted like this:
            //     Entering '<submodule>'
            //     <commit hash>
            string hash = "";
            do
            {
                string line = stdOut.ReadLine();
                if (line.Contains("YARG.Core"))
                {
                    hash = stdOut.ReadLine();
                    break;
                }
            }
            while (!stdOut.EndOfStream);
            if (string.IsNullOrEmpty(hash))
                throw new Exception($"Failed to get commit hash! Command output:\n{stdOut.ReadToEnd()}{error}");

            return hash;
        }
    }
}