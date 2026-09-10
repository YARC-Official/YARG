using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace YARG.Helpers.UI
{
    /// <summary>
    ///     Scrolls an index-based list (the music library, history) by
    ///     dragging it on a touchscreen: every row height of finger travel
    ///     steps the selection one row. Taps on the rows keep working — uGUI
    ///     cancels a click once its pointer starts a drag.
    /// </summary>
    public class ListDragScroller : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        /// <summary>Row height in canvas units.</summary>
        public Func<float> RowHeight;

        /// <summary>Called with +1 to select the next row, -1 the previous.</summary>
        public Action<int> Step;

        private Canvas _canvas;
        private float _travel;

        private void Awake()
        {
            _canvas = GetComponentInParent<Canvas>();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _travel = 0f;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (RowHeight == null || Step == null)
            {
                return;
            }

            float rowPixels = RowHeight() * (_canvas != null ? _canvas.rootCanvas.scaleFactor : 1f);
            if (rowPixels <= 0f)
            {
                return;
            }

            // Dragging the list up (positive screen-space delta) reveals the rows below
            _travel += eventData.delta.y;
            while (_travel >= rowPixels)
            {
                Step(1);
                _travel -= rowPixels;
            }

            while (_travel <= -rowPixels)
            {
                Step(-1);
                _travel += rowPixels;
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _travel = 0f;
        }
    }
}
