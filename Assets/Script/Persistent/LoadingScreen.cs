using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using YARG.Core.Logging;
using YARG.Integration;
using YARG.Localization;
using YARG.Menu.Navigation;
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

            // Load language
            try
            {
                await LocalizationManager.LoadLanguage(context);
            }
            catch (Exception e)
            {
                YargLogger.LogException(e);
            }

            // Load Discord right after (this requires localization)
            try
            {
                DiscordController.Instance.Initialize();
            }
            catch (Exception e)
            {
                YargLogger.LogException(e);
            }

            // Load song sources and icons
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

            // Auto connect profiles, using the same order that they were previously connected.
            if (SettingsManager.Settings.ReconnectProfiles.Value)
            {
                PlayerContainer.AutoConnectProfiles();
            }
            else
            {
                PlayerContainer.ClearProfileOrder();
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
            GC.Collect();
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
