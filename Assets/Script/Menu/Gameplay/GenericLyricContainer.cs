using System.Collections.Generic;
using TMPro;
using UnityEngine;
using YARG.Core.Chart;
using YARG.PlayMode;
using YARG.Settings;

namespace YARG.Menu
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

        private List<VocalsPhrase> _lyrics;
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

            // Disable updates until the song starts
            enabled = false;
            // Temporarily disabled during the rewrite/transition to YARG.Core
            // Play.OnChartLoaded += OnChartLoaded;
            // Play.OnSongStart += OnSongStart;
        }

        // private void OnChartLoaded(SongChart chart)
        // {
        //     Play.OnChartLoaded -= OnChartLoaded;

        //     // Temporary
        //     bool playingVocals = false;
        //     foreach (var player in PlayerManager.players)
        //     {
        //         if (player.chosenInstrument is Instrument.Vocals or Instrument.Harmony)
        //         {
        //             playingVocals = true;
        //         }
        //     }

        //     // Disable if there are no lyrics or someone is singing
        //     _lyrics = chart.GenericLyrics;
        //     if (_lyrics.Count <= 0 || playingVocals)
        //     {
        //         gameObject.SetActive(false);
        //     }
        // }

        // private void OnSongStart(SongEntry song)
        // {
        //     Play.OnSongStart -= OnSongStart;

        //     // Enable updates
        //     enabled = true;
        // }

        private void Update()
        {
            if (_lyricIndex >= _lyrics.Count)
            {
                return;
            }

            var phrase = _lyrics[_lyricIndex];
            if (_lyricPhraseIndex >= phrase.Lyrics.Count && phrase.Bounds.TimeEnd < Play.Instance.SongTime)
            {
                // Clear phrase
                _lyricText.text = string.Empty;

                _lyricPhraseIndex = 0;
                _lyricIndex++;
            }
            else if (_lyricPhraseIndex < phrase.Lyrics.Count &&
                phrase.Lyrics[_lyricPhraseIndex].Time < Play.Instance.SongTime)
            {
                // Consolidate lyrics
                string o = "<color=#5CB9FF>";
                for (int i = 0; i < phrase.Lyrics.Count; i++)
                {
                    string str = phrase.Lyrics[i].Text;

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