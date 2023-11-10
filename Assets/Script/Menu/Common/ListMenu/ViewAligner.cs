using System.Collections;
using UnityEngine;
using YARG.Helpers.Extensions;

namespace YARG.Menu.ListMenu
{
    public class ViewAligner : MonoBehaviour
    {
        [HideInInspector]
        public RectTransform SelectedView;

        [SerializeField]
        private RectTransform _innerViewContainer;

        public void RequestAlignView()
        {
            var currentAnchorPos = _innerViewContainer.anchoredPosition;

            var outerRect = GetComponent<RectTransform>().ToScreenSpace().center.y;
            var selectedRect = SelectedView.ToScreenSpace().center.y;

            // Account for the offset of the inner container
            selectedRect -= currentAnchorPos.y;

            // Offset the inner container
            var difference = outerRect - selectedRect;
            _innerViewContainer.anchoredPosition = currentAnchorPos.WithY(difference);
        }
    }
}