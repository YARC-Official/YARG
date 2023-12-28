using System.Collections;
using Cysharp.Threading.Tasks.Triggers;
using DG.Tweening;
using DG.Tweening.Core.Easing;
using TMPro;
using UnityEditorInternal;
using UnityEngine;
using YARG.Gameplay.Player;
using YARG.Helpers;
using YARG.Settings;

namespace YARG.Gameplay.HUD
{
    public class SongInfoText : GameplayBehaviour
    {
        [SerializeField]
        private CanvasGroup _canvasGroup;

        [SerializeField]
        private TextMeshProUGUI _text;

        [SerializeField]
        private CanvasGroup _albumCanvasOnePlayer;
        [SerializeField]
        private TextMeshProUGUI _albumTextOnePlayer;
        [SerializeField]
        private CanvasGroup _albumCanvasTwoPlayer;
        [SerializeField]
        private TextMeshProUGUI _albumTextTwoPlayer;
        [SerializeField]
        private CanvasGroup _albumCanvasThreePlayer;
        [SerializeField]
        private TextMeshProUGUI _albumTextThreePlayer;

        private void Start()
        {   
            
            if (!SettingsManager.Settings.DisablePerSongBackgrounds.Value)
        {
            // Album Bg has different font parameters
            if (SettingsManager.Settings.AlbumArtBackground.Value)
        {   
            
            {
                _text.gameObject.SetActive(false);
                
                int playerCount = 0;
                foreach (var player in GameManager.Players)
                {
                    if (player is not Gameplay.Player.VocalsPlayer)
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
                        $"<size=100%><font-weight=800>{aLine.Text}</font-weight></size>",
                    SongToText.Style.SubHeader =>
                        $"<size=95%><alpha=#95><i><font-weight=650>{aLine.Text}</font-weight></i></size>",
                    _ =>
                        $"<size=90%><alpha=#80><font-weight=650>{aLine.Text}</font-weight></size>"
                } + "\n";
            }
            _albumTextOnePlayer.text = aFinalText;
            _albumTextTwoPlayer.text = aFinalText;
            _albumTextThreePlayer.text = aFinalText;

            if (playerCount == 1)
            {
                _albumTextOnePlayer.gameObject.SetActive(true);
                _albumTextTwoPlayer.gameObject.SetActive(false);
                _albumTextThreePlayer.gameObject.SetActive(false);
                
            }
            if (playerCount == 2)
            {
                _albumTextOnePlayer.gameObject.SetActive(false);
                _albumTextTwoPlayer.gameObject.SetActive(true);
                _albumTextThreePlayer.gameObject.SetActive(false);
                
            }
            if (playerCount >= 3)
            {
                _albumTextOnePlayer.gameObject.SetActive(false);
                _albumTextTwoPlayer.gameObject.SetActive(false);
                _albumTextThreePlayer.gameObject.SetActive(true);
                
            }
            }
        }
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
            _albumTextOnePlayer.gameObject.SetActive(false);
            _albumTextTwoPlayer.gameObject.SetActive(false);
            _albumTextThreePlayer.gameObject.SetActive(false);

            _text.text = finalText;
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