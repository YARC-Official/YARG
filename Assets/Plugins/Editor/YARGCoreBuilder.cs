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
        private const string OUTPUT_FOLDER = "Assets/Plugins/YARG.Core";
        private const string HASH_PATH = "Assets/Plugins/YARG.Core/YARG.Core.hash";

        // For automatically building in the Editor upon any recompilations
        static YARGCoreBuilder()
        {
            // Don't do anything if entering play mode
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            BuildYARGCoreDLL();
        }

        // For automatically building upon creating a Player build
        public int callbackOrder => -10000;
        public void OnPreprocessBuild(BuildReport report)
        {
            BuildYARGCoreDLL(force: true, debug: false);
        }

        [MenuItem("YARG/Rebuild YARG.Core", false)]
        public static void BuildButton() => BuildYARGCoreDLL(force: true);

        public static void BuildYARGCoreDLL(bool force = false, bool debug = true)
        {
            try
            {
                // Ensure output directory exists
                if (!Directory.Exists(OUTPUT_FOLDER))
                    Directory.CreateDirectory(OUTPUT_FOLDER);

                // Check the current commit hash
                EditorUtility.DisplayProgressBar("Building YARG.Core", "Checking Git commit hash", 0f);
                if (!force && !CheckCommitHash())
                    return;

                Debug.Log("Rebuilding YARG.Core...");

                // Get directories
                string projectRoot = Directory.GetParent(Application.dataPath)?.ToString();
                string submodulePath = Path.Join(projectRoot, "YARG.Core", "YARG.Core");
                string projectPath = Path.Join(submodulePath, "YARG.Core.csproj");

                // Ensure all package references are resolved in Unity
                EditorUtility.DisplayProgressBar("Building YARG.Core", "Restoring packages", 0.1f);
                var packages = RestorePackages(projectPath);

                // Build the project
                EditorUtility.DisplayProgressBar("Building YARG.Core", "Building project", 0.4f);
                string buildOutput = BuildProject(projectPath, debug);
                Debug.Log($"Built YARG.Core to {buildOutput}");

                // Copy output files to plugin folder
                // TODO: Ignore Unity-provided references
                EditorUtility.DisplayProgressBar("Building YARG.Core", "Copying files", 0.9f);
                CopyBuildOutput(buildOutput, OUTPUT_FOLDER, packages);
            }
            catch (Exception ex)
            {
                Debug.LogError("Error while building YARG.Core!");
                Debug.LogException(ex);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static bool CheckCommitHash()
        {
            // Check the current commit hash
            string currentHash = GetCurrentCommitHash();
            if (File.Exists(HASH_PATH) && File.ReadAllText(HASH_PATH) == currentHash)
                return false;

            // Store new commit hash
            if (string.IsNullOrEmpty(currentHash))
                Debug.LogWarning("Failed to read commit hash! Forcing a rebuild.");
            else
                File.WriteAllText(HASH_PATH, currentHash);

            return true;
        }

        private static void CopyBuildOutput(string buildOutput, string destination, string[] existingReferences)
        {
            foreach (var path in Directory.EnumerateFiles(buildOutput, "*.dll"))
            {
                // Check if the .dll already exists as a reference
                string name = Path.GetFileNameWithoutExtension(path);
                if (name != "YARG.Core" && existingReferences.Contains(name))
                    continue;

                // Copy .dll
                string newPath = Path.Combine(destination, $"{name}.dll");
                File.Copy(path, newPath, overwrite: true);

                // Copy .pdb if present
                string pdbName = $"{name}.pdb";
                string pdbPath = Path.Combine(buildOutput, pdbName);
                if (File.Exists(pdbPath))
                {
                    File.Copy(pdbPath, Path.Combine(destination, pdbName), overwrite: true);
                }

                // Import YARG.Core immediately
                if (name == "YARG.Core")
                {
                    AssetDatabase.ImportAsset(newPath);
                }
            }
            Debug.Log($"Copied files to {destination}");
        }

        private static string[] RestorePackages(string projectFilePath)
        {
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

                // Check for an existing installed package
                var packageIdentifier = new NugetPackageIdentifier(packageName, packageVersion);
                if (NugetHelper.InstalledPackages.Any((package) =>
                    package.Title == packageName && package.InRange(packageIdentifier)))
                    continue;

                // Search for the package on NuGet
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
                    break;
                }
            }

            return NugetHelper.InstalledPackages.Select((package) => package.Title).ToArray();
        }

        private static string BuildProject(string projectFile, bool debug)
        {
            // Fire up `dotnet` to publish the project
            string command = debug ? "build" : "publish";
            var output = RunCommand("dotnet",
                @$"{command} ""{projectFile}"" /property:GenerateFullPaths=true /consoleloggerparameters:NoSummary");

            string outputPath = "";
            do
            {
                string line = output.ReadLine();
                if (line is null)
                    break;

                const string search = "YARG.Core -> ";
                int index = line.IndexOf(search);
                if (index >= 0)
                {
                    var path = line.AsSpan().Slice(index + search.Length);
                    if (debug)
                    {
                        // Debug builds only output one path, which is for the built assembly
                        outputPath = Path.GetDirectoryName(path).ToString();
                        break;
                    }
                    else
                    {
                        // Publish builds output two paths: one for the built assembly, and one for the publish folder
                        if (path.EndsWith(".dll"))
                            // This is the built assembly path
                            continue;

                        // This is the publish folder path, we want to copy from here
                        outputPath = path.ToString();
                        break;
                    }
                }
            }
            while (!output.EndOfStream);

            return outputPath;
        }

        private static string GetCurrentCommitHash()
        {
            // Ask Git what the current hash is for each submodule
            // (no way to target just a specific submodule, as far as I can tell)
            var output = RunCommand("git", @"submodule foreach ""git rev-parse HEAD""");

            // Find the line that has the commit hash
            // The output is formatted like this:
            //     Entering '<submodule>'
            //     <commit hash>
            string hash = "";
            do
            {
                string line = output.ReadLine();
                if (line is null)
                    break;

                if (line.Contains("YARG.Core"))
                {
                    hash = output.ReadLine();
                    break;
                }
            }
            while (!output.EndOfStream);

            if (string.IsNullOrEmpty(hash))
                throw new Exception($"Failed to get commit hash! Command output:\n{output.ReadToEnd()}");

            return hash;
        }

        private static StreamReader RunCommand(string command, string args)
        {
            // Run the command
            var process = Process.Start(new ProcessStartInfo()
            {
                FileName = $"{command}.exe",
                Arguments = args,
                UseShellExecute = false, // Must be false to redirect input/output/error
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            });
            process.WaitForExit();

            // Bail out on error
            var output = process.StandardOutput;
            string error = process.StandardError.ReadToEnd();
            if (!string.IsNullOrEmpty(error) || process.ExitCode != 0)
                throw new Exception($"Error when running command! Exit code: {process.ExitCode}, command output:\n{output.ReadToEnd()}{error}");

            return output;
        }
    }
}