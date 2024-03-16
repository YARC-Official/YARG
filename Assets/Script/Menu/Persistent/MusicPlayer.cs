using System.Diagnostics.CodeAnalysis;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Settings;
using YARG.Helpers.Extensions;
using YARG.Core.Audio;
using YARG.Core.Song;
using YARG.Song;

namespace YARG.Menu.Persistent
{
    public class MusicPlayer : MonoBehaviour
    {
        private static SongEntry _nowPlaying = null;
        public static SongEntry NowPlaying => _nowPlaying;

        private StemMixer _mixer = null;

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

        // "The Unity message 'OnEnable' has an incorrect signature."
        [SuppressMessage("Type Safety", "UNT0006", Justification = "UniTaskVoid is a compatible return type.")]
        private async UniTaskVoid OnEnable()
        {
            _songText.text = string.Empty;
            _artistText.text = string.Empty;

            // Wait until the loading is done
            await UniTask.WaitUntil(() => !LoadingScreen.IsActive);

            // Disable if there are no songs to play
            if (SongContainer.Count <= 0)
            {
                gameObject.SetActive(false);
                return;
            }
            await NextSong();
        }

        private void OnDisable()
        {
            _mixer?.Dispose();
            Stop();
        }

        private async UniTask NextSong()
        {
            _mixer?.Dispose();

            _nowPlaying = SongContainer.GetRandomSong();
            _mixer = await UniTask.RunOnThreadPool(() => _nowPlaying.LoadAudio(AudioManager.Instance, 1f, SongStem.Crowd));
            _mixer.SongEnd += OnSongEnd;

            // Set song title text
            _songText.text = _nowPlaying.Name;
            _artistText.text = _nowPlaying.Artist;

            Play();
        }

        private void OnSongEnd()
        {
            NextSong().Forget();
        }

        private void Play()
        {
            _mixer.Play();
            UpdateVolume();
            UpdatePlayOrPauseSprite();
        }

        private void Pause()
        {
            _mixer.Pause();
            UpdatePlayOrPauseSprite();
        }

        private void Stop()
        {
            _mixer.Dispose();
            _mixer = null;
        }

        public void UpdateVolume()
        {
            if (_mixer != null && _mixer.IsPlaying && gameObject.activeSelf)
            {
                _mixer.SetVolume(SettingsManager.Settings.MusicPlayerVolume.Value);
            }
        }

        private void UpdatePlayOrPauseSprite()
        {
            if (_mixer != null && _mixer.IsPlaying)
            {
                _playPauseButton.sprite = _pauseSprite;
            }
            else
            {
                _playPauseButton.sprite = _playSprite;
            }
        }

        public void PlayOrPauseClick()
        {
            if (_mixer == null)
            {
                return;
            }

            if (_mixer.IsPlaying)
            {
                Pause();
            }
            else
            {
                Play();
            }
        }

        public void SkipClick()
        {
            NextSong().Forget();
        }
    }
}