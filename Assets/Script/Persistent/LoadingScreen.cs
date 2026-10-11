using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Core.Logging;
using YARG.Helpers;
using YARG.Input.Bindings;
using YARG.Integration;
using YARG.Localization;
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

        protected override void SingletonAwake()
        {
            // This object owns an override-sorting canvas, so the raycaster on the
            // parent canvas cannot use its full-screen image as an input blocker.
            if (GetComponent<GraphicRaycaster>() == null)
            {
                gameObject.AddComponent<GraphicRaycaster>();
            }
        }

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

            // Check for bad paths
            if (PathHelper.PathError)
            {
                // We may well not be able to localize, so don't even try
                DialogManager.Instance.ShowMessage("Error creating persistent data directory", $"YARG was unable to create persistent data directory: \n\n{CommandLineArgs.PersistentDataPath}\n\nThis is an unrecoverable error, so YARG will exit.");
                await DialogManager.Instance.WaitUntilCurrentClosed();
                Quit();
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

            // Load sources and genre mappings concurrently
            await UpdateSourcesAndGenres(context);

            // Auto connect profiles, using the same order that they were previously connected.
            if (SettingsManager.Settings.ReconnectProfiles.Value)
            {
                PlayerContainer.AutoConnectProfiles();
                RetryUnresolvedMicrophones().Forget();
            }
            else
            {
                PlayerContainer.ClearProfileOrder();
            }

            // Initialize phoneme dictionary (must load on main thread, parse on thread pool)
            var cmudictAsset = Resources.Load<TextAsset>("cmudict");
            if (cmudictAsset == null)
            {
                YargLogger.LogError("Failed to load cmudict.txt from Resources");
            }
            else
            {
                var dictText = cmudictAsset.text;
                await UniTask.RunOnThreadPool(() =>
                {
                    YARG.Core.Chart.LipsyncGenerator.Initialize(dictText);
                    YargLogger.LogInfo("Initialized phoneme dictionary");
                });
            }

            // Fast scan (cache read) on startup
            await SongContainer.RunRefresh(true, context);
        }

        private static async UniTask UpdateSourcesAndGenres(LoadingContext context)
        {
            var tasks = new List<string> { "Song Sources", "Genres" };
            context.SetLoadingText("Updating Song Sources and Genres...");

            RefreshText();

            try
            {
                await UniTask.WhenAll(
                    TaskWrapper(SongSources.LoadSources(), "Song Sources"),
                    TaskWrapper(Genrelizer.LoadGenreMappings(), "Genres")
                    );
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex);
            }

            return;

            async UniTask TaskWrapper(UniTask task, string name)
            {
                try
                {
                    await task;
                }
                finally
                {
                    tasks.Remove(name);
                    RefreshText();
                }
            }

            void RefreshText()
            {
                if (tasks.Count > 0)
                {
                    context.SetSubText(string.Join(", ", tasks));
                }
            }
        }

        private static async UniTaskVoid RetryUnresolvedMicrophones()
        {
            await UniTask.Delay(2000);
            BindingsContainer.ResolveMicrophones();
        }

        private void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
			Application.Quit();
#endif
        }
    }

    public sealed class LoadingContext : IDisposable
    {
        private static int _activeContextCount;

        private bool _disposed;
        private readonly IDisposable _inputBlocker;

        private struct QueuedTask
        {
            public string Text;
            public string SubText;
            public UniTask Task;
        }

        private readonly Queue<QueuedTask> _queue = new();

        public LoadingContext()
        {
            _inputBlocker = Navigator.Instance.PushInputBlocker();

            _activeContextCount++;
            LoadingScreen.Instance.gameObject.SetActive(true);
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
            if (_disposed) return;

            _disposed = true;

            try
            {
                await Wait();
            }
            finally
            {
                _inputBlocker.Dispose();

                _activeContextCount = Math.Max(0, _activeContextCount - 1);
                if (_activeContextCount == 0)
                {
                    LoadingScreen.Instance.gameObject.SetActive(false);
                }

                GC.SuppressFinalize(this);
            }
        }

        ~LoadingContext()
        {
            YargLogger.LogError("Loading context was not disposed!");
            // Disposing is not safe here, as GC is done on a separate thread
        }
    }
}
