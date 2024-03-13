using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using YARG.Core.Logging;
using YARG.Core.Song.Cache;
using YARG.Helpers;
using YARG.Menu.MusicLibrary;
using YARG.Menu.Navigation;
using YARG.Settings;
using YARG.Song;

namespace YARG
{
    public class LoadingManager : MonoSingleton<LoadingManager>
    {
        private struct QueuedTask
        {
            public string Text;
            public string SubText;
            public Func<UniTask> Function;
        }

        [SerializeField]
        private TextMeshProUGUI _loadingPhrase;
        [SerializeField]
        private TextMeshProUGUI _subPhrase;

        public bool IsLoading { get; private set; }

        private readonly Queue<QueuedTask> _loadQueue = new();
        private bool _didPushEmptyScheme;

        // "The Unity message 'Start' has an incorrect signature."
        [SuppressMessage("Type Safety", "UNT0006", Justification = "UniTaskVoid is a compatible return type.")]
        private async UniTaskVoid Start()
        {
            Queue(() => SongSources.LoadSources(SetSubText), "Loading song sources...");

            // Fast scan (cache read) on startup
            QueueSongRefresh(true);

            await StartLoad();
        }

        private void OnEnable()
        {
            Navigator.Instance.DisableMenuInputs = true;
        }

        private void OnDisable()
        {
            Navigator.Instance.DisableMenuInputs = false;
        }

        public async UniTask StartLoad()
        {
            if (_loadQueue.Count <= 0)
            {
                return;
            }

            gameObject.SetActive(true);
            IsLoading = true;

            while (_loadQueue.Count > 0)
            {
                var task = _loadQueue.Dequeue();
                SetLoadingText(task.Text, task.SubText);

                try
                {
                    await task.Function();
                }
                catch (Exception ex)
                {
                    YargLogger.LogException(ex);
                }
            }

            gameObject.SetActive(false);
            IsLoading = false;
        }

        /// <summary>
        /// Adds a parallelizable action to the loading queue.
        /// </summary>
        /// <remarks>
        /// Only add tasks that don't explicitly require the main thread.
        /// </remarks>
        public void Queue(UniTask task, string title = "Loading...", string sub = null)
        {
            Queue(() => task, title, sub);
        }

        /// <summary>
        /// Adds an deferred awaitable function to the loading queue.
        /// </summary>
        /// <remarks>
        /// Preferred for actions that must run on the main thread.
        /// </remarks>
        public void Queue(Func<UniTask> func, string title = "Loading...", string sub = null)
        {
            var task = new QueuedTask
            {
                Text = title,
                SubText = sub,
                Function = func,
            };
            _loadQueue.Enqueue(task);
        }

        /// <summary>
        /// Adds a song scan to the loading queue.
        /// </summary>
        /// <param name="fast">
        /// Whether or not the scan should be a fast or full one.
        /// </param>
        public void QueueSongRefresh(bool fast)
        {
            Queue(() => ScanSongFolders(fast), "Loading songs...");
        }

        private async UniTask ScanSongFolders(bool fast)
        {
            const bool MULTITHREADING = true;
            var directories = SettingsManager.Settings.SongFolders;

            // Handle official setlist invisibly if it is installed
            string setlistPath = PathHelper.SetlistPath;
            if (!string.IsNullOrEmpty(setlistPath) && !directories.Contains(setlistPath))
            {
                directories.Add(setlistPath);
            }

            // Time the scan so we can log it
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Scan and pass in settings
            var task = Task.Run(() => CacheHandler.RunScan(
                fast, PathHelper.SongCachePath, PathHelper.BadSongsPath,
                MULTITHREADING,
                SettingsManager.Settings.AllowDuplicateSongs.Value,
                SettingsManager.Settings.UseFullDirectoryForPlaylists.Value,
                directories));

            while (!task.IsCompleted)
            {
                UpdateSongUI();
                await UniTask.NextFrame();
            }

            stopwatch.Stop();

            YargLogger.LogFormatInfo("Scan time: {0}s", stopwatch.Elapsed.TotalSeconds);

            // Remove official setlist path so it doesn't show up in the list of folders
            if (!string.IsNullOrEmpty(setlistPath))
            {
                directories.Remove(setlistPath);
            }

            GlobalVariables.Instance.SongContainer = new SongContainer(task.Result);
            MusicLibraryMenu.SetRefresh();
        }

        private void SetLoadingText(string phrase, string sub = null)
        {
            _loadingPhrase.text = phrase;
            _subPhrase.text = sub;
        }

        private void SetSubText(string sub)
        {
            _subPhrase.text = sub;
        }

        private void UpdateSongUI()
        {
            var tracker = CacheHandler.Progress;

            string phrase = string.Empty;
            string subText = null;
            switch (tracker.Stage)
            {
                case ScanStage.LoadingCache:
                    phrase = "Loading song cache...";
                    break;
                case ScanStage.LoadingSongs:
                    phrase = "Loading songs...";
                    break;
                case ScanStage.Sorting:
                    phrase = "Sorting songs...";
                    break;
                case ScanStage.WritingCache:
                    phrase = "Writing song cache...";
                    break;
                case ScanStage.WritingBadSongs:
                    phrase = "Writing bad songs...";
                    break;
            }

            switch (tracker.Stage)
            {
                case ScanStage.LoadingCache:
                case ScanStage.LoadingSongs:
                    subText = $"Folders Scanned: {tracker.NumScannedDirectories}\n" +
                              $"Songs Scanned: {tracker.Count}\n" +
                              $"Errors: {tracker.BadSongCount}"; break;
            }
            SetLoadingText(phrase, subText);
        }
    }
}