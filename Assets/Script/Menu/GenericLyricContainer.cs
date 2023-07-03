using System.Collections.Generic;
using TMPro;
using UnityEngine;
using YARG.Data;
using YARG.PlayMode;
using YARG.Settings;
using YARG.Song;

namespace YARG.UI
{
    public class GenericLyricContainer : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI _lyricText;

        [Space]
        [SerializeField]
        private GameObject _normalBackground;

        [SerializeField]
        private GameObject _transparentBackground;

        private List<GenericLyricInfo> _lyrics;
        private int _lyricIndex;
        private int _lyricPhraseIndex;

        private void Start()
        {
            _lyricText.text = string.Empty;

            // Set proper background
            switch (SettingsManager.Settings.LyricBackground.Data)
            {
                case "Normal":
                    _normalBackground.SetActive(true);
                    _transparentBackground.SetActive(false);
                    break;
                case "Transparent":
                    _normalBackground.SetActive(false);
                    _transparentBackground.SetActive(true);
                    break;
                case "None":
                    _normalBackground.SetActive(false);
                    _transparentBackground.SetActive(false);
                    break;
            }

            if (Play.Instance.SongStarted)
            {
                OnSongStart();
            }
            else
            {
                // Disable updates until the song starts
                enabled = false;
                Play.OnSongStart += OnSongStart;
            }
        }

        private void OnSongStart(SongEntry song)
        {
            Play.OnSongStart -= OnSongStart;

            // Enable updates
            enabled = true;

            OnSongStart();
        }

        private void OnSongStart()
        {
            // Temporary
            bool playingVocals = false;
            foreach (var player in PlayerManager.players)
            {
                if (player.chosenInstrument is "vocals" or "harmVocals")
                {
                    playingVocals = true;
                }
            }

            // Disable if there are no lyrics or someone is singing
            _lyrics = Play.Instance.chart.genericLyrics;
            if (_lyrics.Count <= 0 || playingVocals)
            {
                gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            if (_lyricIndex >= _lyrics.Count)
            {
                return;
            }

            var lyric = _lyrics[_lyricIndex];
            if (_lyricPhraseIndex >= lyric.lyric.Count && lyric.EndTime < Play.Instance.SongTime)
            {
                // Clear phrase
                _lyricText.text = string.Empty;

                _lyricPhraseIndex = 0;
                _lyricIndex++;
            }
            else if (_lyricPhraseIndex < lyric.lyric.Count &&
                lyric.lyric[_lyricPhraseIndex].time < Play.Instance.SongTime)
            {
                // Consolidate lyrics
                string o = "<color=#5CB9FF>";
                for (int i = 0; i < lyric.lyric.Count; i++)
                {
                    (_, string str) = lyric.lyric[i];

                    if (str.EndsWith("-"))
                    {
                        o += str[..^1].Replace("=", "-");
                    }
                    else
                    {
                        o += str.Replace("=", "-") + " ";
                    }

                    if (i + 1 > _lyricPhraseIndex)
                    {
                        o += "</color>";
                    }
                }

                _lyricText.text = o;
                _lyricPhraseIndex++;
            }
        }
    }
}