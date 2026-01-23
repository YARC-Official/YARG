using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.SolutionPersistence.Model;
using Microsoft.VisualStudio.SolutionPersistence.Serializer;
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
                string submoduleFullPath = SubmoduleHelper.SubmoduleRoot;
                if (!Directory.Exists(submoduleFullPath))
                {
                    Debug.LogError("YARG.Core submodule does not exist!");
                    return contents;
                }

                // Need to check format manually instead of using SolutionSerializers.GetSerializerByMoniker,
                // otherwise we can't load from and save to `contents` directly
                SolutionModel sln;
                var openStream = new MemoryStream(Encoding.UTF8.GetBytes(contents));
                if (SolutionSerializers.SlnFileV12.IsSupported(path))
                {
                    sln = SolutionSerializers.SlnFileV12.OpenAsync(openStream, default).Result;
                }
                else if (SolutionSerializers.SlnXml.IsSupported(path))
                {
                    sln = SolutionSerializers.SlnXml.OpenAsync(openStream, default).Result;
                }
                else
                {
                    Debug.LogError("Solution format unsupported!");
                    return contents;
                }

                // Folder paths are required to start and end with a forward slash,
                // and use forward slashes as their directory separator
                string submoduleRelativePath = Path.GetRelativePath(SubmoduleHelper.ProjectRoot, submoduleFullPath);
                submoduleRelativePath = submoduleRelativePath.Replace(Path.DirectorySeparatorChar, '/');
                submoduleRelativePath = submoduleRelativePath.Replace(Path.AltDirectorySeparatorChar, '/');

                var submoduleFolder = sln.FindFolder("/YARG.Core/") ?? sln.AddFolder('/' + submoduleRelativePath + '/');

                // Find submodule projects
                foreach (string projectFile in Directory.EnumerateFiles(submoduleFullPath, "*.csproj", SearchOption.AllDirectories))
                {
                    string projectPath = Path.GetRelativePath(SubmoduleHelper.ProjectRoot, projectFile);
                    if (!IgnoredProjects.Contains(Path.GetFileName(projectPath)) && sln.FindProject(projectPath) == null)
                    {
                        sln.AddProject(projectPath);
                    }
                }

                foreach (var project in sln.SolutionProjects)
                {
                    // Ensure submodule projects are always put into the folder
                    // `contents` does not seem to have folders preserved when passed to us,
                    // so we need to ensure the projects get put back in it
                    if (project.FilePath.StartsWith(submoduleRelativePath))
                    {
                        project.MoveToFolder(submoduleFolder);
                    }

                    // Adjust submodule package projects to be contained inside the submodule folder
                    string projectName = Path.GetFileNameWithoutExtension(project.FilePath);
                    if (projectName.EndsWith(".Package"))
                    {
                        project.MoveToFolder(submoduleFolder);
                    }
                }

                var saveStream = new MemoryStream();
                if (SolutionSerializers.SlnFileV12.IsSupported(path))
                {
                    sln.SerializerExtension = SolutionSerializers.SlnFileV12.CreateModelExtension(new()
                    {
                        // We need the solution to be written as UTF-8
                        // so we can convert its written contents back to a string
                        Encoding = Encoding.UTF8
                    });
                    SolutionSerializers.SlnFileV12.SaveAsync(saveStream, sln, default).Wait();
                }
                else if (SolutionSerializers.SlnXml.IsSupported(path))
                {
                    SolutionSerializers.SlnXml.SaveAsync(saveStream, sln, default).Wait();
                }

                // Apply new solution contents
                contents = Encoding.UTF8.GetString(saveStream.ToArray());
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