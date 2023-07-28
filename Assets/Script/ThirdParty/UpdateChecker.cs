using System;
using System.IO;
using System.Net;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using YARG.Data;
using YARG.Menu;
using YARG.Menu.Persistent;

namespace YARG
{
    public class UpdateChecker : MonoSingleton<UpdateChecker>
    {
        private CancellationTokenSource updateTokenSource;

        public bool CheckedForUpdates { get; private set; }
        public bool IsOutOfDate { get; private set; }

        public YargVersion LatestVersion { get; private set; }

        private async UniTask Start()
        {
            enabled = false;
            await CheckForUpdates();
        }

        private async UniTask CheckForUpdates()
        {
            // #if UNITY_EDITOR
            // return;
            // #endif
            updateTokenSource = new CancellationTokenSource();

            Debug.Log("Checking for updates");
            CheckedForUpdates = true;

            try
            {
                // Setup request
                var request =
                    (HttpWebRequest) WebRequest.Create(
                        "https://api.github.com/repos/YARC-Official/YARG/releases/latest");
                request.UserAgent = "YARG";

                // Sets up a cancellation token to cancel the request if needed (such as menu being hidden)
                await using (updateTokenSource.Token.Register(() => request.Abort(), useSynchronizationContext: false))
                {
                    var response = await request.GetResponseAsync();
                    updateTokenSource.Token.ThrowIfCancellationRequested();

                    if (response is null)
                    {
                        throw new WebException("Failed to get response from Update check");
                    }

                    // Read response
                    string json = await new StreamReader(response.GetResponseStream()!).ReadToEndAsync();

                    // Get version tag
                    var jsonObject = JsonConvert.DeserializeObject<JToken>(json);
                    var releaseTag = jsonObject["tag_name"]!.Value<string>();

                    LatestVersion = YargVersion.Parse(releaseTag);

                    if (GlobalVariables.CurrentVersion < LatestVersion)
                    {
                        IsOutOfDate = true;

                        Debug.Log($"Update available! New version: {releaseTag}");
                        ToastManager.ToastInformation($"Update available! New version: {releaseTag}");
                    }
                    else
                    {
                        Debug.Log("Game is up to date.");
                        ToastManager.ToastMessage("Game is up to date.");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError("Failed to check for updates!");
                Debug.LogException(e);
            }
            finally
            {
                updateTokenSource.Dispose();
                updateTokenSource = null;
            }
        }
    }
}
