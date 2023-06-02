using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using YARG.Settings;
using YARG.Song;

namespace YARG.Audio {
	public class PreviewContext {

		public double PreviewStartTime { get; private set; }
		public double PreviewEndTime { get; private set; }

		public bool IsPlaying { get; private set; }

		private readonly IAudioManager _manager;

		private UniTask _loopTask;

		public PreviewContext(IAudioManager manager) {
			_manager = manager;
		}

		private async UniTask StartLooping(float volume, CancellationToken cancelToken) {
			try {
				while (!cancelToken.IsCancellationRequested) {
					_manager.SetPosition(PreviewStartTime);
					_manager.FadeIn(volume);

					await UniTask.WaitUntil(() => cancelToken.IsCancellationRequested || (_manager.IsPlaying &&
						(_manager.CurrentPositionD >= PreviewEndTime || _manager.CurrentPositionD >= _manager.AudioLengthD)));

					await _manager.FadeOut();
				}
			} catch (Exception e) {
				Debug.LogError("Error while looping song preview!");
				Debug.LogException(e);
			}
		}

		public async UniTask PlayPreview(SongEntry song, float volume, CancellationToken cancelToken) {
			// Skip if preview shouldn't be played
			if (song == null || Mathf.Approximately(volume, 0f)) {
				return;
			}

			// Previews must be cancelled before attempting to start a new one
			if (IsPlaying) {
				Debug.LogError($"Attempted to play a new preview without cancelling the previous! Song: {song.Artist} - {song.Name}");
				return;
			}

			IsPlaying = true;

			try {
				// Wait for a X milliseconds to prevent spam loading (no one likes Music Library lag)
				await UniTask.Delay(TimeSpan.FromMilliseconds(300.0), ignoreTimeScale: true);

				// Check if cancelled
				if (cancelToken.IsCancellationRequested) {
					return;
				}

				// Load the song
				await UniTask.RunOnThreadPool(() => {
					if (song is ExtractedConSongEntry conSong) {
						_manager.LoadMogg(conSong, 1f, SongStem.Crowd);
					} else {
						_manager.LoadSong(AudioHelpers.GetSupportedStems(song.Location), 1f, SongStem.Crowd);
					}
				});

				// Check if cancelled
				if (cancelToken.IsCancellationRequested) {
					_manager.UnloadSong();
					return;
				}

				// Set preview start and end times
				PreviewStartTime = song.PreviewStartTimeSpan.TotalSeconds;
				if (PreviewStartTime <= 0.0) {
					PreviewStartTime = 10.0;
				}
				PreviewEndTime = song.PreviewEndTimeSpan.TotalSeconds;
				if (PreviewEndTime <= 0.0) {
					PreviewEndTime = PreviewStartTime + Constants.PREVIEW_DURATION;
				}

				// Play the audio
				await StartLooping(volume, cancelToken);
			} catch (Exception ex) {
				Debug.LogError("Error while playing song preview!");
				Debug.LogException(ex);
			} finally {
				IsPlaying = false;
			}
		}
	}
}