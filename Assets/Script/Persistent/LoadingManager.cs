using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using YARG.Core.Song.Cache;
using YARG.Helpers;
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

        private readonly Queue<QueuedTask> _loadQueue = new();

        private async UniTask Start()
        {
            Queue(async () => await SongSources.LoadSources(SetSubText), "Loading song sources...");

            // Fast scan (cache read) on startup
            QueueSongRefresh(true);

            Queue(ScoreManager.FetchScores, "Reading scores...");

            await StartLoad();
        }

        public async UniTask StartLoad()
        {
            if (_loadQueue.Count <= 0)
            {
                return;
            }

            gameObject.SetActive(true);

            while (_loadQueue.Count > 0)
            {
                var task = _loadQueue.Dequeue();
                SetLoadingText(task.Text, task.SubText);
                await task.Function();
            }

            gameObject.SetActive(false);

#if UNITY_EDITOR
            // Test Play stuff
            // StartTestPlayMode();
#endif
        }

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
            Queue(async () => await ScanSongFolders(fast), "Loading songs...");
        }

        private async UniTask ScanSongFolders(bool fast)
        {
            var directories = SettingsManager.Settings.SongFolders;

            // Handle official setlist invisibly if it is installed
            string setlistPath = PathHelper.SetlistPath;
            if (!string.IsNullOrEmpty(setlistPath) && !directories.Contains(setlistPath))
                directories.Add(setlistPath);

#if UNITY_EDITOR
            CacheHandler handler = new(PathHelper.PersistentDataPath, PathHelper.PersistentDataPath, true, directories.ToArray());
#else
            CacheHandler handler = new(PathHelper.PersistentDataPath, PathHelper.ExecutablePath, true, directories.ToArray());
#endif

            SongCache cache = null;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var task = Task.Run(() => cache = handler.RunScan(fast));
            while (!task.IsCompleted)
            {
                UpdateSongUi(handler);
                await UniTask.NextFrame();
            }
            stopwatch.Stop();

            Debug.Log($"Scan time: {stopwatch.Elapsed.TotalSeconds}s");
            foreach (var err in handler.errorList)
                Debug.LogError(err);

            // Remove official setlist path so it doesn't show up in the list of folders
            if (!string.IsNullOrEmpty(setlistPath))
                directories.Remove(setlistPath);

            GlobalVariables.Instance.SetSongList(cache);
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

        private void UpdateSongUi(CacheHandler cache)
        {
            string phrase = string.Empty;
            string subText = null;
            switch (cache.Progress)
            {
                case ScanProgress.LoadingCache:
                    phrase = "Loading song cache...";
                    break;
                case ScanProgress.LoadingSongs:
                    phrase = "Loading songs...";
                    break;
                case ScanProgress.Sorting:
                    phrase = "Sorting songs...";
                    break;
                case ScanProgress.WritingCache:
                    phrase = "Writing song cache...";
                    break;
                case ScanProgress.WritingBadSongs:
                    phrase = "Writing bad songs...";
                    break;
            }

            switch (cache.Progress)
            {
                case ScanProgress.LoadingCache:
                case ScanProgress.LoadingSongs:
                    subText = $"Folders Scanned: {cache.NumScannedDirectories}\n" +
                              $"Songs Scanned: {cache.Count}\n" +
                              $"Errors: {cache.BadSongCount}"; break;
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