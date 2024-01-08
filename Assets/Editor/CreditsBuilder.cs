using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using YARG.Menu.Credits;

namespace Editor
{
    public class CreditsBuilder : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            CreditsMenu.DownloadCredits();
        }
    }
}