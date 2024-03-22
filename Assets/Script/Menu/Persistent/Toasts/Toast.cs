using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace YARG.Menu.Persistent
{
    public class Toast : MonoBehaviour
    {
        [SerializeField]
        private Image _icon;
        [SerializeField]
        private TextMeshProUGUI _title;
        [SerializeField]
        private TextMeshProUGUI _message;
        [SerializeField]
        private Image _background;
        [SerializeField]
        private CanvasGroup _canvasGroup;

        public void Initialize(string type, string message, Sprite icon, Color color)
        {
            _background.color = color;

            _title.text = type;
            _title.color = color;
            _icon.sprite = icon;

            _message.color = Color.white;
            _message.text = message;

            StartCoroutine(ToastCoroutine());
        }

        private IEnumerator ToastCoroutine()
        {
            // Fade in
            _canvasGroup.alpha = 0f;
            yield return _canvasGroup
                .DOFade(1f, 0.25f)
                .SetUpdate(true)
                .WaitForCompletion();

            // Wait
            yield return new WaitForSecondsRealtime(5f);

            // Fade out
            yield return _canvasGroup
                .DOFade(0f, 0.25f)
                .SetUpdate(true)
                .WaitForCompletion();

            var parentRect = transform.parent.GetComponent<RectTransform>();

            // Smoothly move all of the next notifications up
            // The spacing is embedded into the toast so we don't have to worry about that
            yield return transform
                .DOScaleY(0f, 0.4f)
                .SetUpdate(true)
                .OnUpdate(() =>
                {
                    // Slow, but not much we can do
                    LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
                })
                .WaitForCompletion();

            Destroy(gameObject);
        }
    }
}