using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.AsyncOperations;
using YARG.Core.Logging;
using YARG.Settings;

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

        AsyncOperationHandle<IResourceLocator> _catalogHandle = default;

        // A UniTaskCompletionSource, rather than a cached UniTask, so concurrent and repeated awaits are safe.
        private readonly UniTaskCompletionSource<bool> _catalogTcs = new();

        private bool _loadStarted;

        private void Start()
        {
            WaitForCatalogAsync().Forget();
        }

        public UniTask<bool> WaitForCatalogAsync()
        {
            if (GlobalVariables.OfflineMode || !SettingsManager.Settings.AllowRemoteContent.Value)
            {
                return UniTask.FromResult(false);
            }

            if (!_loadStarted)
            {
                _loadStarted = true;
                LoadCatalogAsync().Forget();
            }

            return _catalogTcs.Task;
        }

        private async UniTaskVoid LoadCatalogAsync()
        {
            bool loaded = false;
            try
            {
                var url = GetCatalogUrl();
                if (url != null)
                {
                    loaded = await Initialize(url);
                }
            }
            catch (Exception ex)
            {
                YargLogger.LogFormatWarning("Failed to load remote content catalog: {0}", ex.Message);
            }
            finally
            {
                _catalogTcs.TrySetResult(loaded);
            }
        }

        private async UniTask<bool> Initialize(string url)
        {
            try
            {
                await Addressables.InitializeAsync();

                _catalogHandle = Addressables.LoadContentCatalogAsync(url, false);
                await _catalogHandle.Task;

                if (_catalogHandle.Status != AsyncOperationStatus.Succeeded)
                {
                    YargLogger.LogFormatWarning("Failed to load remote content catalog: {0}", url);
                    return false;
                }

                YargLogger.LogFormatDebug("Loaded remote content catalog: {0}", url);

                AsyncOperationHandle<bool> cleanerHandle = default;
                try
                {
                    cleanerHandle = Addressables.CleanBundleCache();
                    await cleanerHandle.Task;
                }
                catch (Exception ex)
                {
                    YargLogger.LogFormatWarning("Failed to clean bundle cache: {0}", ex.Message);
                }
                finally
                {
                    if (cleanerHandle.IsValid())
                    {
                        Addressables.Release(cleanerHandle);
                    }
                }
            }
            catch (Exception ex)
            {
                YargLogger.LogFormatWarning("Failed to load remote content catalog: {0}", ex.Message);
                return false;
            }
            finally
            {
                if (_catalogHandle.IsValid())
                {
                    Addressables.Release(_catalogHandle);
                    _catalogHandle = default;
                }
            }

            return true;
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