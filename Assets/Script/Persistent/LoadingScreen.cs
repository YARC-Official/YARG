using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using YARG.Core.Logging;
using YARG.Menu.Navigation;
using YARG.Song;

namespace YARG
{
    public class LoadingScreen : MonoSingleton<LoadingScreen>
    {
        public TextMeshProUGUI LoadingPhrase;
        public TextMeshProUGUI SubPhrase;

        public static bool IsActive => Instance.gameObject.activeSelf;

        // "The Unity message 'Start' has an incorrect signature."
        [SuppressMessage("Type Safety", "UNT0006", Justification = "UniTaskVoid is a compatible return type.")]
        private async UniTaskVoid Start()
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

        private async UniTaskVoid _Dispose()
        {
            if (!_disposed)
            {
                await Wait();
                LoadingScreen.Instance.gameObject.SetActive(false);
                Navigator.Instance.DisableMenuInputs = false;
                _disposed = true;
            }
        }

        ~LoadingContext()
        {
            YargLogger.LogError("Loading context was not disposed!");
            // Disposing is not safe here, as GC is done on a separate thread
            // _Dispose().Forget();
        }

        public void Dispose()
        {
            _Dispose().Forget();
            GC.SuppressFinalize(this);
        }
    }
}