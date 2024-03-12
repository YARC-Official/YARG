using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using YARG.Menu.Navigation;
using YARG.Song;
using YARG.Core.Logging;

namespace YARG
{
    public class LoadingScreen : MonoSingleton<LoadingScreen>
    {
        [SerializeField]
        public TextMeshProUGUI loadingPhrase;

        [SerializeField]
        public TextMeshProUGUI subPhrase;

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
        }
    }

    public sealed class LoadingContext : IDisposable
    {
        private static object _contextLock = new();
        private static int _refCount = 0;
        private bool disposedValue;

        private struct QueuedTask
        {
            public string Text;
            public string SubText;
            public UniTask Task;
        }

        private readonly Queue<QueuedTask> _queue = new();

        public static bool IsLoading()
        {
            lock (_contextLock)
            {
                return _refCount > 0;
            }
        }

        public LoadingContext()
        {
            lock (_contextLock)
            {
                if (_refCount == 0)
                {
                    LoadingScreen.Instance.gameObject.SetActive(true);
                    Navigator.Instance.DisableMenuInputs = true;
                }
                ++_refCount;
            }
        }

        public void SetLoadingText(string phrase, string sub = null)
        {
            LoadingScreen.Instance.loadingPhrase.text = phrase;
            LoadingScreen.Instance.subPhrase.text = sub;
        }

        public void SetSubText(string sub)
        {
            LoadingScreen.Instance.subPhrase.text = sub;
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

        private async void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                await Wait();
                lock (_contextLock)
                {
                    --_refCount;
                    if (_refCount == 0)
                    {
                        LoadingScreen.Instance.gameObject.SetActive(false);
                        Navigator.Instance.DisableMenuInputs = false;
                    }
                }
                disposedValue = true;
            }
        }

        ~LoadingContext()
        {
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}