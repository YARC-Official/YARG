using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Xml;
using NugetForUnity;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

using Debug = UnityEngine.Debug;

namespace Editor
{
    [InitializeOnLoad]
    public class YARGCoreBuilder : AssetPostprocessor, IPreprocessBuildWithReport
    {
        private const string OUTPUT_FOLDER = "Assets/Plugins/YARG.Core";
        private const string HASH_PATH = OUTPUT_FOLDER + "/YARG.Core.hash";
        private const string REBUILD_LOCK_PATH = OUTPUT_FOLDER + "/__reload_lock";

        // NugetForUnity doesn't expose their list of Unity pre-installed references,
        // and I'd rather not re-implement it ourselves lol
        private delegate HashSet<string> UnityPreImportedLibraryResolver_GetAlreadyImportedLibs();
        private static readonly UnityPreImportedLibraryResolver_GetAlreadyImportedLibs s_GetAlreadyImportedLibs =
            (UnityPreImportedLibraryResolver_GetAlreadyImportedLibs) Assembly.GetAssembly(typeof(NugetHelper))
            .GetType("NugetForUnity.UnityPreImportedLibraryResolver").GetMethod("GetAlreadyImportedLibs",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, new Type[] { }, null)
            .CreateDelegate(typeof(UnityPreImportedLibraryResolver_GetAlreadyImportedLibs));

        private static string ProjectRoot => Directory.GetParent(Application.dataPath)?.ToString();

        // For automatically building in the Editor upon any recompilations
        static YARGCoreBuilder()
        {
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;

            // Don't rebuild when entering play mode or reloading assemblies
            if (EditorApplication.isPlayingOrWillChangePlaymode || File.Exists(REBUILD_LOCK_PATH))
                return;

            BuildYARGCoreDLL();
        }

        // For automatically building upon creating a Player build
        public int callbackOrder => -10000;
        public void OnPreprocessBuild(BuildReport report)
        {
            BuildYARGCoreDLL(force: true, debug: false);
        }

        [MenuItem("YARG/Rebuild YARG.Core (Debug)", false, 0)]
        public static void BuildDebug() => BuildYARGCoreDLL(force: true);

        [MenuItem("YARG/Rebuild YARG.Core (Release)", false, 0)]
        public static void BuildRelease() => BuildYARGCoreDLL(force: true, debug: false);

        private static void OnBeforeAssemblyReload()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload -= OnAfterAssemblyReload;

            if (!File.Exists(REBUILD_LOCK_PATH))
                File.Create(REBUILD_LOCK_PATH).Dispose();
        }

        private static void OnAfterAssemblyReload()
        {
            if (File.Exists(REBUILD_LOCK_PATH))
                File.Delete(REBUILD_LOCK_PATH);
        }

        // Undocumented post-process hooks called by IDE packages
        // For the first two, return can be either void if no content modifications are made,
        // or string if the contents are being modified

        // Adds YARG.Core projects to the Unity solution
        // Avoids having to switch between solutions constantly
        private static string OnGeneratedSlnSolution(string path, string contents)
        {
            // Check for submodule
            string projectRoot = ProjectRoot;
            string submodule = Path.Combine(projectRoot, "YARG.Core");
            if (!Directory.Exists(submodule))
            {
                Debug.LogError("YARG.Core submodule does not exist!");
                return contents;
            }

            // Write to temporary file
            string directory = Path.GetDirectoryName(path);
            string tempFile = Path.Combine(directory, "temp.sln");
            File.WriteAllText(tempFile, contents);

            // Find submodule projects
            // Collected separately so we can have a count
            var projectFiles = new List<string>();
            EditorUtility.DisplayProgressBar("Adding YARG.Core Projects to Solution", "Finding project files", 0f);
            foreach (string folder in Directory.EnumerateDirectories(submodule, "*.*", SearchOption.TopDirectoryOnly))
            {
                foreach (string projectFile in Directory.EnumerateFiles(folder, "*.csproj", SearchOption.TopDirectoryOnly))
                {
                    projectFiles.Add(projectFile);
                }
            }

            // Add submodule projects
            for (int i = 0; i < projectFiles.Count; i++)
            {
                string projectFile = projectFiles[i];
                try
                {
                    RunCommand("dotnet", @$"sln ""{tempFile}"" add ""{projectFile}""",
                        "Adding YARG.Core Projects to Solution",
                        $"Adding {Path.GetFileName(projectFile)} ({i + 1} of {projectFiles.Count})",
                        (float) i / projectFiles.Count
                    ).Dispose();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to add YARG.Core project {projectFile} to solution {path}");
                    Debug.LogException(ex);
                }
            }
            EditorUtility.ClearProgressBar();

            // Read back temp file as new contents
            contents = File.ReadAllText(tempFile);
            File.Delete(tempFile);
            return contents;
        }

        // Adds YARG.Core to each of the Unity project files
        // Not really necessary and takes a long time, presumably since the project files are large
        // private static string OnGeneratedCSProject(string path, string contents)
        // {
        //     // Check for submodule
        //     string projectRoot = ProjectRoot;
        //     string submodule = Path.Combine(projectRoot, "YARG.Core");
        //     if (!Directory.Exists(submodule))
        //     {
        //         Debug.LogWarning($"Submodule {"YARG.Core"} does not exist!");
        //         return contents;
        //     }

        //     // Write to temporary file
        //     string directory = Path.GetDirectoryName(path);
        //     string tempFile = Path.Combine(directory, "temp.csproj");
        //     File.WriteAllText(tempFile, contents);

        //     // Add YARG.Core reference
        //     try
        //     {
        //         EditorUtility.DisplayProgressBar("Adding YARG.Core Reference to Project",
        //             $"Adding YARG.Core to {Path.GetFileName(path)}", 0f);
        //         string projectFile = Path.Join(submodule, "YARG.Core", $"YARG.Core.csproj");
        //         RunCommand("dotnet", @$"add ""{tempFile}"" reference ""{projectFile}""").Dispose();
        //     }
        //     catch (Exception ex)
        //     {
        //         Debug.LogError($"Failed to add YARG.Core project to project {path}");
        //         Debug.LogException(ex);
        //     }
        //     finally
        //     {
        //         EditorUtility.ClearProgressBar();
        //     }

        //     // Read back temp file as new contents
        //     contents = File.ReadAllText(tempFile);
        //     File.Delete(tempFile);
        //     return contents;
        // }

        public static void BuildYARGCoreDLL(bool force = false, bool debug = true)
        {
            try
            {
                // Ensure output directory exists
                if (!Directory.Exists(OUTPUT_FOLDER))
                    Directory.CreateDirectory(OUTPUT_FOLDER);

                // Check the current commit hash
                if (!force && !CheckCommitHash(progress: 0f))
                    return;

                Debug.Log("Rebuilding YARG.Core...");

                // Get directories
                string projectRoot = ProjectRoot;
                string submodulePath = Path.Join(projectRoot, "YARG.Core", "YARG.Core");
                string projectPath = Path.Join(submodulePath, "YARG.Core.csproj");

                // Ensure all package references are resolved in Unity
                var packages = RestorePackages(projectPath, progressStart: 0.1f, progressEnd: 0.4f);

                // Build the project
                string buildOutput = BuildProject(projectPath, debug, progress: 0.4f);
                Debug.Log($"Built YARG.Core to {buildOutput}");

                // Copy output files to plugin folder
                // TODO: Ignore Unity-provided references
                EditorUtility.DisplayProgressBar("Building YARG.Core", "Copying files", 0.9f);
                CopyBuildOutput(buildOutput, OUTPUT_FOLDER, packages);

                EditorUtility.DisplayProgressBar("Building YARG.Core", "Removing conflicting files", 0.95f);
                RemoveConflictingFromOutput(buildOutput, packages);
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Error while building YARG.Core!", ex.ToString(), "OK");
                Debug.LogError("Error while building YARG.Core!");
                Debug.LogException(ex);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static bool CheckCommitHash(float progress)
        {
            // Check the current commit hash
            string currentHash = GetCurrentCommitHash(progress);
            if (File.Exists(HASH_PATH) && File.ReadAllText(HASH_PATH) == currentHash)
                return false;

            // Store new commit hash
            if (string.IsNullOrEmpty(currentHash))
                Debug.LogWarning("Failed to read commit hash! Forcing a rebuild.");
            else
                File.WriteAllText(HASH_PATH, currentHash);

            return true;
        }

        private static void CopyBuildOutput(string buildOutput, string destination, HashSet<string> existingReferences)
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

        private static void RemoveConflictingFromOutput(string buildOutput, HashSet<string> existingReferences)
        {
            foreach (var path in Directory.EnumerateFiles(buildOutput, "*.dll"))
            {
                // Check if the .dll already exists as a reference
                string name = Path.GetFileNameWithoutExtension(path);
                if (name == "YARG.Core" || !existingReferences.Contains(name))
                    continue;

                // Remove the .dll
                File.Delete(path);
            }
        }

        private static HashSet<string> RestorePackages(string projectFilePath, float progressStart, float progressEnd)
        {
            // Load project file
            var projectFile = new XmlDocument();
            projectFile.Load(projectFilePath);

            // Load Nuget config file, NugetForUnity is strange with this lol
            if (NugetHelper.NugetConfigFile is null)
                NugetHelper.LoadNugetConfigFile();

            // Find package references
            var existingReferences = s_GetAlreadyImportedLibs();
            var packageReferences = projectFile.GetElementsByTagName("PackageReference");
            for (int index = 0; index < packageReferences.Count; index++)
            {
                float progress = Mathf.Lerp(progressStart, progressEnd, (float) index / packageReferences.Count);
                EditorUtility.DisplayProgressBar("Building YARG.Core", "Restoring packages", progress);

                // Get package info
                var packageReference = packageReferences[index];
                string packageName = packageReference.Attributes["Include"].Value;
                string packageVersion = packageReference.Attributes["Version"].Value;

                // Check for an existing installed package.
                // Don't use `package.InRange` because it incorrectly compares beta versions.
                var existingPackage = NugetHelper.InstalledPackages.FirstOrDefault((package) =>
                    package.Id == packageName && package.Version == packageVersion);
                if (existingPackage is not null)
                {
                    // Add to references
                    GetPackageDlls(existingPackage, existingReferences);
                    continue;
                }

                // Search for the package on NuGet
                foreach (var package in NugetHelper.Search(packageName))
                {
                    if (package.Title != packageName || package.Version != packageVersion)
                        continue;

                    // Install the package
                    Debug.Log($"Installing {package.Title} v{package.Version}");
                    if (!NugetHelper.Install(package))
                    {
                        Debug.LogWarning($"Failed to install {package.Title} v{package.Version}!");
                        continue;
                    }

                    // Add to references
                    GetPackageDlls(package, existingReferences);
                    break;
                }
            }

            return existingReferences;
        }

        private static void GetPackageDlls(NugetPackage package, HashSet<string> existingReferences)
        {
            // Note: This assumes there is only one target framework folder at a time in the package's install folder
            // NugetForUnity seems to only install one at a time, but there may be issues if more than one exists
            string installFolder = Path.Combine(NugetHelper.NugetConfigFile.RepositoryPath, $"{package.Id}.{package.Version}");
            foreach (var path in Directory.EnumerateFiles(installFolder, "*.dll", SearchOption.AllDirectories))
            {
                string name = Path.GetFileNameWithoutExtension(path);
                if (existingReferences.Contains(name))
                    continue;

                existingReferences.Add(name);
            }
        }

        private static string BuildProject(string projectFile, bool debug, float progress)
        {
            // Fire up `dotnet` to publish the project
            string command = debug ? "build" : "publish";
            var output = RunCommand("dotnet",
                @$"{command} ""{projectFile}"" /nologo /p:GenerateFullPaths=true /consoleloggerparameters:NoSummary",
                "Building YARG.Core", "Building project", progress);

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

        private static string GetCurrentCommitHash(float progress)
        {
            // Ask Git what the current hash is for each submodule
            // (no way to target just a specific submodule, as far as I can tell)
            var output = RunCommand("git", @"submodule foreach ""git rev-parse HEAD""",
                "Building YARG.Core", "Checking Git commit hash", progress);

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

        private static StreamReader RunCommand(string command, string args, string progMsg, string progInfo, float progress)
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
            });

            while (!process.HasExited)
            {
                if (EditorUtility.DisplayCancelableProgressBar(progMsg, progInfo, progress))
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
                throw new Exception($"Error when running command! Exit code: {process.ExitCode}, command output:\n{output.ReadToEnd()}{error}");

            return output;
        }
    }
}