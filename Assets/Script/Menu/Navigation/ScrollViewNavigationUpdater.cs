using UnityEngine;
using UnityEngine.UI;
using YARG.Helpers.Extensions;

namespace YARG.Menu.Navigation
{
    [RequireComponent(typeof(NavigationGroup), typeof(ScrollRect))]
    public class ScrollViewNavigationUpdater : MonoBehaviour
    {
        [SerializeField]
        private RectTransform _contentTransform;

        private NavigationGroup _navigationGroup;
        private ScrollRect _scrollRect;
        private RectTransform _viewportTransform;

        private void Awake()
        {
            _navigationGroup = GetComponent<NavigationGroup>();
            _scrollRect = GetComponent<ScrollRect>();
            _viewportTransform = _scrollRect.viewport != null
                ? _scrollRect.viewport
                : GetComponent<RectTransform>();

            _navigationGroup.SelectionChanged += OnSelectionChanged;
        }

        private void OnSelectionChanged(NavigatableBehaviour selected, SelectionOrigin selectionOrigin)
        {
            // Only scroll it automatically if it's a navigation selection type
            if (selectionOrigin != SelectionOrigin.Navigation || selected == null)
                return;

            Canvas.ForceUpdateCanvases();

            if (_scrollRect.ScrollableHeight() <= 0f)
            {
                _scrollRect.verticalNormalizedPosition = 1f;
                return;
            }

            var selectedTransform = selected.transform as RectTransform;
            if (selectedTransform == null) return;

            var viewportBounds = new Bounds(_viewportTransform.rect.center, _viewportTransform.rect.size);
            var selectedBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                _viewportTransform, selectedTransform);

            var newPos = _contentTransform.anchoredPosition.y;
            if (selectedBounds.max.y > viewportBounds.max.y)
            {
                newPos -= selectedBounds.max.y - viewportBounds.max.y;
            }
            else if (selectedBounds.min.y < viewportBounds.min.y)
            {
                newPos += viewportBounds.min.y - selectedBounds.min.y;
            }
            else
            {
                return;
            }

            newPos = Mathf.Clamp(newPos, 0f, _scrollRect.ScrollableHeight());
            _contentTransform.anchoredPosition = _contentTransform.anchoredPosition.WithY(newPos);
        }

        private void OnDestroy()
        {
            _navigationGroup.SelectionChanged -= OnSelectionChanged;
        }
    }
}