using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using YARG.Core.Game;
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
            SongSources.LoadSprites(context);

            // If we want to reconnect profiles
            if (SettingsManager.Settings.ReconnectProfiles.Value)
            {
                // Try to connect any profile that has AutoConnect true
                foreach (var profile in PlayerContainer.Profiles.Where(p => p.AutoConnect))
                {
                    ConnectToProfile(profile);
                }
            }
            else
            {
                // Otherwise clear the AutoConnect (to match what would be currently connected)
                foreach (var profile in PlayerContainer.Profiles)
                {
                    profile.AutoConnect = false;
                }
            }
        }

        private static void ConnectToProfile(YargProfile profile)
        {
            // Create player from profile
            var player = PlayerContainer.CreatePlayerFromProfile(profile, true);
            // If we not were successful in creating a player
            if (player is null)
            {
                // Then something went wrong, we were unable to connect to the profile.
                ToastManager.ToastWarning($"Unable to connect to profile {profile.Name}.");
            }
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