using System;
using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace YARG.Menu.Persistent
{
    public class Toast : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private const float FADE_DURATION = 0.25f;
        private const float HOVER_FADE = 0.98f;

        private const float SHRINK_DURATION = 0.3f;

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

        private bool _canInteract;
        private Coroutine _coroutine;
        private Action _onClick;

        public void Initialize(string type, string message, Sprite icon, Color color, Action onClick)
        {
            _background.color = color;

            _title.text = type;
            _title.color = color;
            _icon.sprite = icon;

            _message.color = Color.white;
            _message.text = message;

            _onClick = onClick;

            _coroutine = StartCoroutine(ToastStartCoroutine());
        }

        private IEnumerator ToastStartCoroutine()
        {
            _canInteract = false;

            // Fade in
            _canvasGroup.alpha = 0f;
            yield return _canvasGroup
                .DOFade(1f, 0.25f)
                .SetUpdate(true)
                .WaitForCompletion();

            _canInteract = true;

            // Wait
            yield return new WaitForSecondsRealtime(5f);

            // Toast
            yield return ToastEndCoroutine();
        }

        private IEnumerator ToastEndCoroutine()
        {
            _canInteract = false;

            // Fade out
            yield return _canvasGroup
                .DOFade(0f, FADE_DURATION)
                .SetUpdate(true)
                .WaitForCompletion();

            var parentRect = transform.parent.GetComponent<RectTransform>();

            // Smoothly move all of the next notifications up
            // The spacing is embedded into the toast so we don't have to worry about that
            yield return transform
                .DOScaleY(0f, SHRINK_DURATION)
                .SetUpdate(true)
                .OnUpdate(() =>
                {
                    // Slow, but not much we can do
                    LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
                })
                .WaitForCompletion();

            Destroy(gameObject);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!_canInteract)
            {
                return;
            }

            _onClick?.Invoke();

            StopCoroutine(_coroutine);
            _coroutine = StartCoroutine(ToastEndCoroutine());
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!_canInteract)
            {
                return;
            }

            _canvasGroup.DOComplete();
            _canvasGroup.DOFade(HOVER_FADE, FADE_DURATION);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!_canInteract)
            {
                return;
            }

            _canvasGroup.DOComplete();
            _canvasGroup.DOFade(1f, FADE_DURATION);
        }
    }
}