using UnityEngine;
using UnityEngine.UI;

namespace YARG.Menu.Navigation
{
    [RequireComponent(typeof(NavigationGroup), typeof(ScrollRect))]
    public class ScrollViewNavigationUpdater : MonoBehaviour
    {
        [SerializeField]
        private RectTransform _contentTransform;

        private NavigationGroup _navigationGroup;
        private ScrollRect _scrollRect;
        private RectTransform _rectTransform;

        private void Awake()
        {
            _navigationGroup = GetComponent<NavigationGroup>();
            _scrollRect = GetComponent<ScrollRect>();
            _rectTransform = GetComponent<RectTransform>();

            _navigationGroup.SelectionChanged += OnSelectionChanged;
        }

        private void OnSelectionChanged(NavigatableBehaviour selected, SelectionOrigin selectionOrigin)
        {
            // Only scroll it automatically if it's a navigation selection type
            if (selectionOrigin != SelectionOrigin.Navigation || selected == null)
                return;

            Canvas.ForceUpdateCanvases();

            var newPos =
                _scrollRect.transform.InverseTransformPoint(_contentTransform.position).y -
                _scrollRect.transform.InverseTransformPoint(selected.transform.position).y -
                _rectTransform.rect.height / 2f;

            _contentTransform.anchoredPosition = _contentTransform.anchoredPosition.WithY(newPos);
        }

        private void OnDestroy()
        {
            _navigationGroup.SelectionChanged -= OnSelectionChanged;
        }
    }
}