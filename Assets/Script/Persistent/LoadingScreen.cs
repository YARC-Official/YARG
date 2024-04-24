using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using YARG.Core.Logging;
using YARG.Menu.Navigation;
using YARG.Menu.Persistent;
using YARG.Player;
using YARG.Settings;
using YARG.Song;

namespace YARG
{
    public class LoadingScreen : MonoSingleton<LoadingScreen>
    {
        public TextMeshProUGUI LoadingPhrase;
        public TextMeshProUGUI SubPhrase;

        public static bool IsActive => Instance.gameObject.activeSelf;

        private async void Start()
        {
            using var context = new LoadingContext();
            context.SetLoadingText("Loading song sources...");
            try
            {
                await SongSources.LoadSources(context);
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex);
            }

            // Fast scan (cache read) on startup
            await SongContainer.RunRefresh(true, context);

            // Check if we want to connect the first profile
            if (SettingsManager.Settings.ConnectFirstProfile.Value
                // and there are any profiles to connect
                && PlayerContainer.Profiles.Count > 0)
            {
                ConnectToFirstProfile();
            }
        }

        private void ConnectToFirstProfile()
        {
            // Get the first non-bot profile
            var profile = PlayerContainer.Profiles.FirstOrDefault(p => !p.IsBot);
            // Only if there are any non-bot profiles
            if (profile is not null)
            {
                // Create player from profile
                var player = PlayerContainer.CreatePlayerFromProfile(profile, true);
                // If we were successful in creating a player
                if (player is not null && !player.Bindings.Empty)
                {
                    // Success
                    return;
                }
            }

            // Something went wrong and we were unable to connect to the profile.
            ToastManager.ToastWarning("Unable to connect to first profile.");
        }
    }

    public sealed class LoadingContext : IDisposable
    {
        private bool _disposed;

        private struct QueuedTask
        {
            public string Text;
            public string SubText;
            public UniTask Task;
        }

        private readonly Queue<QueuedTask> _queue = new();

        public LoadingContext()
        {
            LoadingScreen.Instance.gameObject.SetActive(true);
            Navigator.Instance.DisableMenuInputs = true;
        }

        public void SetLoadingText(string phrase, string sub = null)
        {
            LoadingScreen.Instance.LoadingPhrase.text = phrase;
            LoadingScreen.Instance.SubPhrase.text = sub;
        }

        public void SetSubText(string sub)
        {
            LoadingScreen.Instance.SubPhrase.text = sub;
        }

        /// <summary>
        /// Adds a parallelizable action to the loading queue
        /// </summary>
        /// <remarks>Only add tasks that don't explicitly require the main thread</remarks>
        public void Queue(UniTask task, string title = "Loading...", string sub = null)
        {
            _queue.Enqueue(new QueuedTask
            {
                Text = title,
                SubText = sub,
                Task = task,
            });
        }

        public async UniTask Wait()
        {
            while (_queue.TryDequeue(out var node))
            {
                SetLoadingText(node.Text, node.SubText);

                try
                {
                    await node.Task;
                }
                catch (Exception ex)
                {
                    YargLogger.LogException(ex);
                }
            }
        }

        public async void Dispose()
        {
            if (!_disposed)
            {
                await Wait();
                LoadingScreen.Instance.gameObject.SetActive(false);
                Navigator.Instance.DisableMenuInputs = false;
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }

        ~LoadingContext()
        {
            YargLogger.LogError("Loading context was not disposed!");
            // Disposing is not safe here, as GC is done on a separate thread
        }
    }
}