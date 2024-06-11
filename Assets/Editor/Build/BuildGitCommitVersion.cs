using System.IO;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using YARG;

namespace Editor.Build
{
    public class BuildGitCommitVersion : IPreprocessBuildWithReport
    {
        public int callbackOrder { get; }
        public void OnPreprocessBuild(BuildReport report)
        {
            File.WriteAllText("Assets/Resources/version.txt", GlobalVariables.LoadVersionFromGit());
        }
    }
}