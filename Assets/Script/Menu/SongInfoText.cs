using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using YARG.PlayMode;
using YARG.Util;

namespace YARG.UI
{
    public class SongInfoText : MonoBehaviour
    {
        [SerializeField]
        private CanvasGroup _canvasGroup;

        [SerializeField]
        private TextMeshProUGUI _text;

        private void Start()
        {
            // Start fading out
            StartCoroutine(FadeCoroutine());

            var lines = SongToText.ToStyled(SongToText.FORMAT_LONG, Play.Instance.Song);

            string finalText = "";
            foreach (var line in lines)
            {
                finalText += line.Style switch
                {
                    SongToText.Style.Header => $"<size=100%><font-weight=800>{line.Text}</font-weight></size>",
                    SongToText.Style.SubHeader =>
                        $"<size=90%><alpha=#90><i><font-weight=600>{line.Text}</font-weight></i></size>",
                    _ => $"<size=80%><alpha=#66><i><font-weight=600>{line.Text}</font-weight></i></size>"
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
    }
}