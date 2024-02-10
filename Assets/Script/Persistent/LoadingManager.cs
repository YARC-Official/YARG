using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using YARG.Core.Song.Cache;
using YARG.Helpers;
using YARG.Menu.MusicLibrary;
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
        private TextMeshProUGUI loadingPhrase;

        [SerializeField]
        private TextMeshProUGUI subPhrase;

        public bool IsLoading { get; private set; }

        private readonly Queue<QueuedTask> _loadQueue = new();

        // "The Unity message 'Start' has an incorrect signature."
        [SuppressMessage("Type Safety", "UNT0006", Justification = "UniTaskVoid is a compatible return type.")]
        private async UniTaskVoid Start()
        {
            Queue(() => SongSources.LoadSources(SetSubText), "Loading song sources...");

            // Fast scan (cache read) on startup
            QueueSongRefresh(true);

            await StartLoad();
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
                    Debug.LogException(ex);
                }
            }

            gameObject.SetActive(false);
            IsLoading = false;
        }

        /// <summary>
        /// Adds a parallelizable action to the loading queue
        /// </summary>
        /// <remarks>Only add tasks that don't explicitly require the main thread</remarks>
        /// <param name="Action"></param>
        /// <param name="title"></param>
        /// <param name="sub"></param>
        public void Queue(Action Action, string title = "Loading...", string sub = null)
        {
            var func = UniTask.RunOnThreadPool(Action);
            Queue(() => func, title, sub);  
        }

        /// <summary>
        /// Adds an deferred awaitable function to the loading queue
        /// </summary>
        /// <remarks>Preferred for actions that must run on the main thread</remarks>
        /// <param name="Action"></param>
        /// <param name="title"></param>
        /// <param name="sub"></param>
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

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            var task = Task.Run(() =>
                CacheHandler.RunScan(fast, PathHelper.SongCachePath, PathHelper.BadSongsPath, MULTITHREADING, true, false, directories));

            while (!task.IsCompleted)
            {
                UpdateSongUi();
                await UniTask.NextFrame();
            }

            stopwatch.Stop();

            Debug.Log($"Scan time: {stopwatch.Elapsed.TotalSeconds}s");

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
            loadingPhrase.text = phrase;
            subPhrase.text = sub;
        }

        private void SetSubText(string sub)
        {
            subPhrase.text = sub;
        }

        private void UpdateSongUi()
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

#if UNITY_EDITOR
        // private void StartTestPlayMode()
        // {
        //     var info = GlobalVariables.Instance.TestPlayInfo;
        //
        //     // Skip if not test play mode
        //     if (!info.TestPlayMode)
        //     {
        //         return;
        //     }
        //
        //     info.TestPlayMode = false;
        //
        //     // Add the bots
        //     // if (!info.NoBotsMode)
        //     // {
        //     //     AddTestPlayPlayer(new PlayerManager.Player
        //     //     {
        //     //         chosenInstrument = "guitar",
        //     //         chosenDifficulty = Difficulty.EXPERT,
        //     //         inputStrategy = new FiveFretInputStrategy
        //     //         {
        //     //             // BotMode = true
        //     //         }
        //     //     });
        //     //
        //     //     AddTestPlayPlayer(new PlayerManager.Player
        //     //     {
        //     //         chosenInstrument = "realDrums",
        //     //         chosenDifficulty = Difficulty.EXPERT_PLUS,
        //     //         inputStrategy = new DrumsInputStrategy
        //     //         {
        //     //             // BotMode = true
        //     //         }
        //     //     });
        //     //
        //     //     // AddTestPlayPlayer(new PlayerManager.Player
        //     //     // {
        //     //     //     chosenInstrument = "vocals",
        //     //     //     chosenDifficulty = Difficulty.EXPERT,
        //     //     //     inputStrategy = new MicInputStrategy
        //     //     //     {
        //     //     //         BotMode = true
        //     //     //     }
        //     //     // });
        //     // }
        //
        //     // Get the Test Play song by hash, and play it
        //     if (SongContainer.SongsByHash.TryGetValue(info.TestPlaySongHash,
        //         out var song))
        //     {
        //         GlobalVariables.Instance.CurrentSong = song;
        //         GlobalVariables.Instance.LoadScene(SceneIndex.Gameplay);
        //     }
        // }
        //
        // private static void AddTestPlayPlayer(PlayerManager.Player p)
        // {
        //     PlayerManager.players.Add(p);
        //     // p.inputStrategy.Enable();
        // }
#endif
    }
}