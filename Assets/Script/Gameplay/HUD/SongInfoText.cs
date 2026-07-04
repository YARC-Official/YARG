using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
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

        private void Start()
        {
            if (GameManager.IsPractice)
            {
                // Don't show anything in practice mode
                Destroy(gameObject);
                return;
            }

            if (!SettingsManager.Settings.KeepSongInfoVisible.Value)
            {
                // Start fading out
                StartCoroutine(FadeCoroutine());
            }

            var lines = SongToText.ToStyled(SongToText.FORMAT_LONG, GameManager.Song);

            string finalText = "";
            foreach (var line in lines)
            {
                // Get correct alpha tag for the given style
                string alpha = line.Style switch
                {
                    SongToText.Style.Header => "",
                    SongToText.Style.SubHeader => "<alpha=#90>",
                    _ => "<alpha=#66>"
                };

                string text = AddAlphaReset(line.Text, alpha);

                // Add styles to each styling
                finalText += line.Style switch
                {
                    SongToText.Style.Header =>
                        $"<size=100%>{alpha}<font-weight=800>{text}</font-weight></size>",
                    SongToText.Style.SubHeader =>
                        $"<size=90%>{alpha}<i><font-weight=600>{text}</font-weight></i></size>",
                    _ =>
                        $"<size=80%>{alpha}<i><font-weight=600>{text}</font-weight></i></size>"
                } + "\n";
            }

            _text.text = finalText;
        }

        private IEnumerator FadeCoroutine()
        {
            _canvasGroup.alpha = 1f;

            // Wait for 10 seconds
            yield return new WaitForSeconds(10f);

            // Then fade to 0 in a second
            yield return _canvasGroup.DOFade(0f, 1f).WaitForCompletion();
        }

        private static string AddAlphaReset(string text, string alpha)
        {
            if (string.IsNullOrEmpty(alpha))
            {
                return text;
            }

            return text.Replace("</color>", $"</color>{alpha}");
        }
    }
}