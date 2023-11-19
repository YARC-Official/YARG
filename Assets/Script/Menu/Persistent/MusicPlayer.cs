using System.Diagnostics.CodeAnalysis;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Audio;
using YARG.Settings;
using YARG.Helpers.Extensions;
using YARG.Core.Audio;

namespace YARG.Menu.Persistent
{
    public class MusicPlayer : MonoBehaviour
    {
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
            await UniTask.WaitUntil(() => !LoadingManager.Instance.IsLoading);

            // Disable if there are no songs to play
            if (GlobalVariables.Instance.SongContainer.Songs.Count <= 0)
            {
                gameObject.SetActive(false);
                return;
            }

            GlobalVariables.AudioManager.SongEnd += OnSongEnd;
            await NextSong();
        }

        private void OnDisable()
        {
            GlobalVariables.AudioManager.SongEnd -= OnSongEnd;
            Stop();
        }

        private async UniTask NextSong()
        {
            var song = GlobalVariables.Instance.SongContainer.Songs[Random.Range(0, GlobalVariables.Instance.SongContainer.Songs.Count)];
            await UniTask.RunOnThreadPool(() => song.LoadAudio(GlobalVariables.AudioManager, 1f, SongStem.Crowd));

            // Set song title text
            _songText.text = song.Name;
            _artistText.text = song.Artist;

            Play();
        }

        private void OnSongEnd()
        {
            NextSong().Forget();
        }

        private void Play()
        {
            GlobalVariables.AudioManager.Play();
            UpdateVolume();
            UpdatePlayOrPauseSprite();
        }

        private void Pause()
        {
            GlobalVariables.AudioManager.Pause();
            UpdatePlayOrPauseSprite();
        }

        private void Stop()
        {
            GlobalVariables.AudioManager.UnloadSong();
        }

        public void UpdateVolume()
        {
            if (GlobalVariables.AudioManager.IsPlaying && gameObject.activeSelf)
            {
                GlobalVariables.AudioManager.SetAllStemsVolume(SettingsManager.Settings.MusicPlayerVolume.Data);
            }
        }

        private void UpdatePlayOrPauseSprite()
        {
            if (GlobalVariables.AudioManager.IsPlaying)
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
            if (!GlobalVariables.AudioManager.IsAudioLoaded)
            {
                return;
            }

            if (GlobalVariables.AudioManager.IsPlaying)
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