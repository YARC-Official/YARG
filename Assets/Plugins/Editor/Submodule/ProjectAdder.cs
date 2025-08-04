using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace YARG.Editor.Submodules
{
    [InitializeOnLoad]
    public class ProjectAdder : AssetPostprocessor
    {
        private static readonly HashSet<string> IgnoredProjects = new()
        {
            // Skip YARG.Core itself since it's already included as a package
            "YARG.Core.csproj",
        };

        // Undocumented post-process hook called by IDE packages.
        // Return type can be either void (no modifications) or string (modifications made).
        private static string OnGeneratedSlnSolution(string path, string contents)
        {
            try
            {
                // Check for submodule
                string submodule = SubmoduleHelper.SubmoduleRoot;
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
                foreach (string projectFile in Directory.EnumerateFiles(submodule, "*.csproj", SearchOption.AllDirectories))
                {
                    if (!IgnoredProjects.Contains(Path.GetFileName(projectFile)))
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
                        SubmoduleHelper.RunCommand(
                            "dotnet",
                            @$"sln ""{tempFile}"" add ""{projectFile}""",
                            "Adding YARG.Core Projects to Solution",
                            $"Adding {Path.GetFileName(projectFile)} ({i + 1} of {projectFiles.Count})",
                            (float) i / projectFiles.Count
                        );
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Failed to add YARG.Core project {projectFile} to solution {path}");
                        Debug.LogException(ex);
                    }
                }

                // Read back temp file as new contents
                contents = File.ReadAllText(tempFile);
                File.Delete(tempFile);
            }
            catch (Exception ex)
            {
                Debug.LogError("Error while adding YARG.Core projects to solution!");
                Debug.LogException(ex);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            return contents;
        }
    }
}