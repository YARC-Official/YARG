using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using YARG.Core.Logging;

namespace YARG
{
    /// <summary>
    /// Merges a remote Addressables catalog on startup.
    /// </summary>
    [DefaultExecutionOrder(-4900)]
    public class RemoteCatalogLoader : MonoSingleton<RemoteCatalogLoader>
    {
        // Someday this will live at yarg.in, but today is not that day
        private const string CATALOG_BASE_URL = "https://yarg-assets-{0}.ulna.net/unity-assets/{1}/";
        private const string CATALOG_JSON_FILE = "catalog_0.1.0.json";

        protected override void SingletonAwake()
        {
            if (GlobalVariables.OfflineMode)
            {
                return;
            }

            var url = GetCatalogUrl();
            if (url == null)
            {
                return;
            }

            _ = Initialize(url);
        }

        private async UniTask<bool> Initialize(string url)
        {
            await Addressables.InitializeAsync();

            try
            {
                var handle = Addressables.LoadContentCatalogAsync(url, true);
                await handle.Task;

                if (handle.Status == AsyncOperationStatus.Succeeded)
                {
                    YargLogger.LogFormatDebug("Loaded remote content catalog: {0}", url);
                    Addressables.Release(handle);
                    return true;
                }
            }
            catch (Exception ex)
            {
                YargLogger.LogFormatWarning("Failed to load remote content catalog: {0}", ex.Message);
            }

            return false;
        }

        private static string GetCatalogUrl()
        {
            // Are we dev, nightly, or release?
            var buildType = GlobalVariables.GetReleaseType();

            if (buildType == null)
            {
                return null;
            }

            // And which platform?
            var platform = Application.platform switch
            {
                RuntimePlatform.WindowsPlayer => "StandaloneWindows64",
                RuntimePlatform.WindowsEditor => "StandaloneWindows64",
                RuntimePlatform.LinuxPlayer   => "StandaloneLinux64",
                RuntimePlatform.LinuxEditor   => "StandaloneLinux64",
                RuntimePlatform.OSXPlayer     => "StandaloneOSX",
                RuntimePlatform.OSXEditor     => "StandaloneOSX",
                _                             => null
            };

            if (platform == null)
            {
                // How, we only build for those platforms?
                return null;
            }

            var catalogBaseUrl = string.Format(CATALOG_BASE_URL, buildType, platform) + CATALOG_JSON_FILE;

            return catalogBaseUrl;
        }
    }
}