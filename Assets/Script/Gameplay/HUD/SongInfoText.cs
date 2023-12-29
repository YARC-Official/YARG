using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks.Triggers;
using DG.Tweening;
using DG.Tweening.Core.Easing;
using TMPro;
using UnityEditor.Localization.Plugins.XLIFF.V20;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using YARG.Gameplay.Player;
using YARG.Helpers;
using YARG.Settings;
using YARG.Venue;

namespace YARG.Gameplay.HUD
{
    public class SongInfoText : GameplayBehaviour
    {
        [SerializeField]
        private CanvasGroup _canvasGroup;

        [SerializeField]
        private TextMeshProUGUI _text;

        [SerializeField] 
        private TextMeshProUGUI _albumText;

        private void Start()
        {   
            
                // Album Bg has different font parameters
                if (SettingsManager.Settings.BackgroundMode.Value == VenueMode.AlbumAsBackground || SettingsManager.Settings.BackgroundMode.Value == VenueMode.OverrideToAlbumAsBackground)
                {   
                    int playerCount = 0;
                    foreach (var player in GameManager.Players)
                    {
                        if (player is not VocalsPlayer)
                            playerCount++;
                    } 

                    var aLines = SongToText.ToStyled(SongToText.FORMAT_LONG, GameManager.Song);

                    string aFinalText = "";
                    foreach (var aLine in aLines)
                    {
                        // Add styles to each styling
                        aFinalText += aLine.Style switch
                        {
                            SongToText.Style.Header =>
                                $"<size=110%><font-weight=800>{aLine.Text}</font-weight></size>",
                            SongToText.Style.SubHeader =>
                                $"<size=105%><alpha=#95><i><font-weight=700>{aLine.Text}</font-weight></i></size>",
                            _ =>
                                $"<size=100%><alpha=#80><font-weight=650>{aLine.Text}</font-weight></size>"
                        } + "\n";
                    }

                    _text.gameObject.SetActive(false);
                    _albumText.text = aFinalText;
            
                    if (playerCount == 1)
                    {
                        RectTransform _albumTextRt = _albumText.GetComponent<RectTransform>(); 
                        _albumTextRt.anchoredPosition = new Vector3(415, -600, -2);
                    }
                    if (playerCount == 2)
                    {
                        RectTransform _albumTextRt = _albumText.GetComponent<RectTransform>(); 
                        _albumTextRt.anchoredPosition = new Vector3(-160, -775, -2);
                        string realign = "<align=center>";
                        _albumText.text = $"{realign}{aFinalText}";
                    }
                    if (playerCount >= 3)
                    {
                        RectTransform _albumTextRt = _albumText.GetComponent<RectTransform>(); 
                        _albumTextRt.anchoredPosition = new Vector3 (300, -480, -2);
                    }

                    _albumText.gameObject.SetActive(true);
                }
                else
                {
                    if (!SettingsManager.Settings.KeepSongInfoVisible.Value)
                    {
                        // Start fading out
                        StartCoroutine(FadeCoroutine());
                    }

                    var lines = SongToText.ToStyled(SongToText.FORMAT_LONG, GameManager.Song);

                    string finalText = "";
                    foreach (var line in lines)
                    {
                        // Add styles to each styling
                        finalText += line.Style switch
                        {
                            SongToText.Style.Header =>
                                $"<size=100%><font-weight=800>{line.Text}</font-weight></size>",
                            SongToText.Style.SubHeader =>
                                $"<size=90%><alpha=#90><i><font-weight=600>{line.Text}</font-weight></i></size>",
                            _ =>
                                $"<size=80%><alpha=#66><i><font-weight=600>{line.Text}</font-weight></i></size>"
                        } + "\n";
                    }

                    _albumText.gameObject.SetActive(false);
                    _text.text = finalText;
                    _text.gameObject.SetActive(true);
                }
        }
    
        private IEnumerator FadeCoroutine()
        {
            _canvasGroup.alpha = 1f;

            // Wait for 10 seconds
            yield return new WaitForSeconds(10f);

            // Then fade to 0 in a second
            yield return _canvasGroup.DOFade(0f, 1f).WaitForCompletion();
        }
    }
}