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
			Queue(async () => {
				await ScanSongFolder(path);
			});
		}

		private async UniTask ScanSongFolders(bool fast) {
			SetLoadingText("Loading songs...");
			var errors = await SongContainer.ScanAllFolders(fast, scanner => {
				subPhrase.text = $"Folders Scanned: {scanner.TotalFoldersScanned}" +
					$"\nSongs Scanned: {scanner.TotalSongsScanned}" +
					$"\nErrors: {scanner.TotalErrorsEncountered}";
			});

			// Only scan folders again on fast mode, as on slow mode they were already scanned
			if (fast) {
				foreach (var error in errors) {
					await ScanSongFolder(error);
				}
			}
		}

		private async UniTask ScanSongFolder(string path) {
			SetLoadingText("Loading songs from folder...");
			await SongContainer.ScanSingleFolder(path, scanner => {
				subPhrase.text = $"Folders Scanned: {scanner.TotalFoldersScanned}" +
					$"\nSongs Scanned: {scanner.TotalSongsScanned}" +
					$"\nErrors: {scanner.TotalErrorsEncountered}";
			});
		}

		private void SetLoadingText(string phrase, string sub = null) {
			loadingPhrase.text = phrase;
			subPhrase.text = sub;
		}
	}
}