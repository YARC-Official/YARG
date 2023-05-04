using System;
using System.Collections.Generic;
using System.Linq;
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
			AddToLoadQueue(async () => {
				SetLoadingText("Fetching sources from web...");
				await SongSources.LoadSources();
			});

			AddSongRefreshToLoadQueue(false);

			await Load();
		}

		public async UniTask Load() {
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

		public void AddToLoadQueue(Func<UniTask> func) {
			loadQueue.Enqueue(func);
		}

		public void AddSongRefreshToLoadQueue(bool fast) {
			AddToLoadQueue(async () => {
				await ScanSongFolders(fast);
			});
		}

		private async UniTask ScanSongFolders(bool fast) {
			SetLoadingText("Loading songs...");
			await SongContainer.ScanAllFolders(fast, scanner => {
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