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

        private object _lock = new();
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

        private async void OnEnable()
        {
            _songText.text = _artistText.text = string.Empty;

            // Wait until the loading is done
            await UniTask.WaitUntil(() => !LoadingScreen.IsActive);

            // Disable if there are no songs to play
            if (SongContainer.Count <= 0)
            {
                gameObject.SetActive(false);
                return;
            }
            NextSong();
        }

        private void OnDisable()
        {
            lock (_lock)
            {
                _mixer?.Dispose();
                _mixer = null;
            }
        }

        public async void NextSong()
        {
            StemMixer mixer = null;
            await UniTask.RunOnThreadPool(() =>
            {
                const float SPEED = 1f;
                lock (_lock)
                {
                    _mixer?.Dispose();
                    _nowPlaying = SongContainer.GetRandomSong();
                    mixer = _mixer = _nowPlaying.LoadAudio(SPEED, SettingsManager.Settings.MusicPlayerVolume.Value, SongStem.Crowd);
                    _mixer.SongEnd += NextSong;
                }
            });

            lock (_lock)
            {
                if (mixer != _mixer)
                {
                    return;
                }
                
                _mixer.Play();

                _songText.text = _nowPlaying.Name;
                _artistText.text = _nowPlaying.Artist;
                _playPauseButton.sprite = _pauseSprite;
            }
        }

        public void UpdateVolume(double volume)
        {
            lock (_lock)
            {
                _mixer?.SetVolume(volume);
            }
        }

        public void TogglePlay()
        {
            lock (_lock)
            {
                if (_mixer == null)
                {
                    return;
                }
                
                if (_mixer.IsPlaying)
                {
                    _mixer.Pause();
                    _playPauseButton.sprite = _playSprite;
                }
                else
                {
                    _mixer.Play();
                    _playPauseButton.sprite = _pauseSprite;
                }
            }
        }
    }
}