using UnityEngine;
using UnityEngine.EventSystems;
using YARG.Settings;

namespace YARG.Gameplay.HUD
{
    [RequireComponent(typeof(RectTransform))]
    public class DraggableHudElement : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        [SerializeField]
        private string _draggableElementName;

        private RectTransform _rectTransform;

        private Vector2 _originalPosition;
        private Vector2 _storedPosition;

        private bool _isDragging;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _originalPosition = _rectTransform.anchoredPosition;

            // Need to fetch the saved position from the settings and apply it
            if (SettingsManager.Settings.UiElementPositions.TryGetValue(_draggableElementName,
                out var serializedPosition))
            {
                _storedPosition = serializedPosition;
            }
            else
            {
                SettingsManager.Settings.UiElementPositions.Add(_draggableElementName, _originalPosition);

                _storedPosition = _originalPosition;
            }

            _rectTransform.anchoredPosition = _storedPosition;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            // Can only start dragging with the left mouse button
            if(_isDragging || eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            _isDragging = true;
        }

        public void OnDrag(PointerEventData eventData)
        {
            // Prevent dragging with other buttons (and "double dragging", increases speed and gets weird)
            if (!_isDragging || eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            var position = _rectTransform.anchoredPosition;
            position.x += eventData.delta.x;
            position.y += eventData.delta.y;

            _rectTransform.anchoredPosition = position;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            // Only end the drag if it was started with the left mouse button
            if (!_isDragging || eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            _isDragging = false;

            // Save the position to the settings
            SettingsManager.Settings.UiElementPositions[_draggableElementName] = _rectTransform.anchoredPosition;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                // Don't reset the position if currently dragging (it breaks stuff)
                if (_isDragging)
                {
                    return;
                }

                _rectTransform.anchoredPosition = _originalPosition;
                SettingsManager.Settings.UiElementPositions[_draggableElementName] = _rectTransform.anchoredPosition;
            }
        }
    }
}