using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Audio;
using YARG.Settings;
using YARG.Song;

namespace YARG.UI {
	public class MusicPlayer : MonoBehaviour {
		[SerializeField]
		private Image _playPauseButton;
		[SerializeField]
		private TextMeshProUGUI _songText;
		[SerializeField]
		private TextMeshProUGUI _artistText;

		[Space]
		[SerializeField]
		private Sprite _playSprite;
		[SerializeField]
		private Sprite _pauseSprite;

		private bool _wasPaused;

		private async UniTask OnEnable() {
			_songText.text = string.Empty;
			_artistText.text = string.Empty;

			// Wait until the loading is done
			await UniTask.WaitUntil(() => !LoadingManager.Instance.gameObject.activeSelf);

			// Disable if there are no songs to play
			if (SongContainer.Songs.Count <= 0) {
				gameObject.SetActive(false);
				return;
			}

			await NextSong();
		}

		private void OnDisable() {
			GameManager.AudioManager.UnloadSong();
		}

		private void UpdatePlayOrPauseSprite() {
			if (GameManager.AudioManager.IsPlaying) {
				_playPauseButton.sprite = _pauseSprite;
			} else {
				_playPauseButton.sprite = _playSprite;
			}
		}

		private async UniTask NextSong() {
			var song = SongContainer.Songs[Random.Range(0, SongContainer.Songs.Count)];

			await UniTask.RunOnThreadPool(() => {
				if (song is ExtractedConSongEntry conSong) {
					GameManager.AudioManager.LoadMogg(conSong, 1f, SongStem.Crowd);
				} else {
					GameManager.AudioManager.LoadSong(AudioHelpers.GetSupportedStems(song.Location), 1f, SongStem.Crowd);
				}
			});

			// Set song title text
			_songText.text = song.Name;
			_artistText.text = song.Artist;

			if (!_wasPaused) {
				Play();
			}
		}

		private void Play() {
			GameManager.AudioManager.Play();
			UpdateVolume();
		}

		public void UpdateVolume() {
			if (GameManager.AudioManager.IsPlaying && gameObject.activeSelf) {
				GameManager.AudioManager.SetAllStemsVolume(SettingsManager.Settings.MusicPlayerVolume.Data);
			}
		}

		public void PlayOrPauseClick() {
			if (!GameManager.AudioManager.IsAudioLoaded) {
				return;
			}

			if (GameManager.AudioManager.IsPlaying) {
				_wasPaused = true;
				GameManager.AudioManager.Pause();
			} else {
				_wasPaused = false;
				Play();
			}

			UpdatePlayOrPauseSprite();
		}

		public async void SkipClick() {
			_wasPaused = false;
			await NextSong();
			UpdatePlayOrPauseSprite();
		}
	}
}