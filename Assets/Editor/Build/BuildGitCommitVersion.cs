using System.IO;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using YARG;

namespace Editor.Build
{
    public class BuildGitCommitVersion : IPreprocessBuildWithReport
    {
        private const string OUTPUT_FOLDER = "Assets/Resources";

        public int callbackOrder { get; }
        public void OnPreprocessBuild(BuildReport report)
        {
            if (!Directory.Exists(OUTPUT_FOLDER))
            {
                Directory.CreateDirectory(OUTPUT_FOLDER);
            }

            File.WriteAllText("Assets/Resources/version.txt", GlobalVariables.LoadVersionFromGit());
        }
    }
}