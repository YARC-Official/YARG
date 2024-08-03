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
using System.Threading.Tasks;

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

        private static Task<StemMixer> _current;
        public async void NextSong()
        {
            const int MAX_TRIES = 10;
            for (int tries = 0; tries < MAX_TRIES; tries++)
            {
                var entry = SongContainer.GetRandomSong();
                if (entry == _nowPlaying)
                {
                    continue;
                }
                _nowPlaying = entry;

                Task<StemMixer> task;
                lock (_lock)
                {
                    const float SPEED = 1f;
                    _current = task = Task.Run(() => entry.LoadAudio(SPEED, SettingsManager.Settings.MusicPlayerVolume.Value, SongStem.Crowd));
                }

                var mixer = await task;
                if (mixer == null)
                {
                    continue;
                }

                lock (_lock)
                {
                    if (_current != task || !gameObject.activeSelf)
                    {
                        mixer.Dispose();
                        continue;
                    }

                    _mixer?.Dispose();
                    _mixer = mixer;
                    _mixer.SongEnd += () =>
                    {
                        _mixer.Dispose();
                        _mixer = null;
                        NextSong();
                    };
                    _mixer.Play(true);

                    _songText.text = _nowPlaying.Name;
                    _artistText.text = _nowPlaying.Artist;
                    _playPauseButton.sprite = _pauseSprite;
                }
                return;
            }
            _nowPlaying = null;
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
                
                if (!_mixer.IsPaused)
                {
                    _mixer.Pause();
                    _playPauseButton.sprite = _playSprite;
                }
                else
                {
                    _mixer.Play(false);
                    _playPauseButton.sprite = _pauseSprite;
                }
            }
        }
    }
}