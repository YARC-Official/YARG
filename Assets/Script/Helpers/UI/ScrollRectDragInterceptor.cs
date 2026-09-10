using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace YARG.Helpers.UI
{
    /// <summary>
    /// Thanks Unity...
    /// </summary>
    [RequireComponent(typeof(ScrollRect))]
    public class ScrollRectDragInterceptor : MonoBehaviour, IEndDragHandler, IBeginDragHandler
    {
        private ScrollRect scroll;

        private void Awake()
        {
            scroll = GetComponent<ScrollRect>();

#if UNITY_IOS || UNITY_ANDROID
            // Dragging is the only natural way to scroll on a touchscreen, so
            // keep the ScrollRect's built-in drag handling there. (This
            // component exists to suppress drag scrolling on desktop, where
            // the wheel and controller navigation drive the views.)
            Destroy(this);
#endif
        }

        public void OnBeginDrag(PointerEventData data)
        {
            scroll.StopMovement();
            scroll.enabled = false;
        }

        public void OnEndDrag(PointerEventData data)
        {
            scroll.enabled = true;
        }
    }
}