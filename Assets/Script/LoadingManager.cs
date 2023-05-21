using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using YARG.Data;
using YARG.Song;

namespace YARG {
	public class LoadingManager : MonoBehaviour {
		public static LoadingManager Instance { get; private set; }

		[SerializeField]
		private TextMeshProUGUI loadingPhrase;
		[SerializeField]
		private TextMeshProUGUI subPhrase;

		private Queue<Func<UniTask>> loadQueue = new();

		private void Awake() {
			Instance = this;
		}

		private async UniTask Start() {
			Queue(async () => {
				SetLoadingText("Fetching sources from web...");
				await SongSources.LoadSources();
			});

			// Fast scan (cache read) on startup
			QueueSongRefresh(true);

			Queue(async () => {
				SetLoadingText("Reading scores...");
				await ScoreManager.FetchScores();
			});

			await StartLoad();
		}

		public async UniTask StartLoad() {
			if (loadQueue.Count <= 0) {
				return;
			}

			gameObject.SetActive(true);

			while (loadQueue.Count > 0) {
				var func = loadQueue.Dequeue();
				await func();
			}

			gameObject.SetActive(false);
		}

		public void Queue(Func<UniTask> func) {
			loadQueue.Enqueue(func);
		}

		public void QueueSongRefresh(bool fast) {
			Queue(async () => {
				await ScanSongFolders(fast);
			});
		}

		public void QueueSongFolderRefresh(string path) {
			// Refreshes 1 folder (called when clicking "Refresh" on a folder in settings)
			Queue(async () => {
				await ScanSongFolder(path, false);
			});
		}

		private async UniTask ScanSongFolders(bool fast) {
			SetLoadingText("Loading songs...");
			var errors = await SongContainer.ScanAllFolders(fast, UpdateSongUi);

			// Pass all errored caches in at once so it can run in parallel
			await SongContainer.ScanFolders(errors, false, UpdateSongUi);
		}

		private async UniTask ScanSongFolder(string path, bool fast) {
			SetLoadingText("Loading songs from folder...");
			await SongContainer.ScanSingleFolder(path, fast, UpdateSongUi);
		}

		private void SetLoadingText(string phrase, string sub = null) {
			loadingPhrase.text = phrase;
			subPhrase.text = sub;
		}

		private void UpdateSongUi(SongScanner scanner) {
			string subText = $"Folders Scanned: {scanner.TotalFoldersScanned}" +
							 $"\nSongs Scanned: {scanner.TotalSongsScanned}" +
							 $"\nErrors: {scanner.TotalErrorsEncountered}";

			SetLoadingText("Loading songs...", subText);
		}
	}
}