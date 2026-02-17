using UnityEngine;
using UnityEngine.EventSystems;
using YARG.Helpers.Extensions;
using static UnityEngine.RectTransformUtility;

namespace YARG.Gameplay.HUD
{
    public class ScaleDragHandler
    {
        private readonly RectTransform _targetRect;
        private readonly float _minScale;

        // Distance from handle to center when drag began
        private float _dragStartDistance;
        // Scale value when drag began
        private float _dragStartScale;
        // Offset from click position to handle center
        private Vector2 _dragClickOffset;

        public float CurrentScale { get; private set; }

        public ScaleDragHandler(RectTransform targetRect, float minScale)
        {
            _targetRect = targetRect;
            _minScale = minScale;
            CurrentScale = minScale;
        }

        public void Initialize(float persistedScale)
        {
            CurrentScale = Mathf.Max(_minScale, persistedScale);
        }

        public bool ShouldScale(PointerEventData eventData, RectTransform scaleHandle)
        {
            if (!scaleHandle.gameObject.activeInHierarchy)
            {
                return false;
            }

            var pressedObject = eventData.pointerPressRaycast.gameObject
                ?? eventData.pointerCurrentRaycast.gameObject;
            if (pressedObject == null)
            {
                return false;
            }

            if (pressedObject.transform.IsChildOf(scaleHandle.transform))
            {
                return true;
            }

            return RectangleContainsScreenPoint(
                scaleHandle, eventData.pressPosition, eventData.pressEventCamera);
        }

        public void BeginScaleDrag(PointerEventData eventData, RectTransform scaleHandle)
        {
            var centerScreenPoint = _targetRect.GetScreenCenter(eventData.pressEventCamera);
            var handleScreenPoint = scaleHandle.GetScreenCenter(eventData.pressEventCamera);

            _dragClickOffset = eventData.position - handleScreenPoint;
            _dragStartDistance = Vector2.Distance(handleScreenPoint, centerScreenPoint);
            _dragStartScale = CurrentScale;
        }

        public bool UpdateScale(PointerEventData eventData)
        {
            if (_dragStartDistance < 1f)
            {
                return false;
            }

            var centerScreenPoint = _targetRect.GetScreenCenter(eventData.pressEventCamera);
            float currentDistance = Vector2.Distance(eventData.position - _dragClickOffset, centerScreenPoint);
            float rawScale = _dragStartScale * (currentDistance / _dragStartDistance);
            float newScale = Mathf.Max(_minScale, rawScale);

            if (Mathf.Approximately(CurrentScale, newScale))
            {
                return false;
            }

            CurrentScale = newScale;
            return true;
        }

        public void Reset()
        {
            CurrentScale = _minScale;
        }
    }
}
