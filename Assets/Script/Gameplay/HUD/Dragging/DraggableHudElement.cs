﻿using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using YARG.Settings;

namespace YARG.Gameplay.HUD
{
    [RequireComponent(typeof(RectTransform))]
    public class DraggableHudElement : GameplayBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler,
        IPointerDownHandler
    {
        [SerializeField]
        private string _draggableElementName;
        [SerializeField]
        private UnityEvent<bool> _onEditModeChanged;

        [Space]
        [SerializeField]
        private DraggingDisplay _draggingDisplayPrefab;

        private DraggableHudManager _manager;
        private RectTransform _rectTransform;

        private DraggingDisplay _draggingDisplay;

        private Vector2 _originalPosition;
        private Vector2 _storedPosition;

        private bool _isSelected;
        private bool _isDragging;

        protected override void OnSongStarted()
        {
            if (GameManager.Players.Count > 1)
            {
                enabled = false;
                return;
            }

            _manager = GetComponentInParent<DraggableHudManager>();
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

            _draggingDisplay = Instantiate(_draggingDisplayPrefab, transform);
            _draggingDisplay.DraggableHud = this;

            _draggingDisplay.Hide();
            _draggingDisplay.gameObject.SetActive(false);
        }

        public void Select()
        {
            _isSelected = true;
            _rectTransform.SetAsLastSibling();

            _draggingDisplay.Show();
        }

        public void Deselect()
        {
            _isSelected = false;

            if (_isDragging)
            {
                _isDragging = false;
                SavePosition();
            }

            _draggingDisplay.Hide();
        }

        public void OnEditModeChanged(bool on)
        {
            _draggingDisplay.gameObject.SetActive(on);
            _onEditModeChanged.Invoke(on);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            // Can only start dragging with the left mouse button
            if (!_manager.EditMode || _isDragging || eventData.button != PointerEventData.InputButton.Left)
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
            SavePosition();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!_manager.EditMode || _isSelected || eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            _manager.SetSelectedElement(this);
        }

        public void RevertElement()
        {
            _rectTransform.anchoredPosition = _storedPosition;
            SavePosition();
        }

        public void ResetElement()
        {
            _rectTransform.anchoredPosition = _originalPosition;
            SavePosition();
        }

        private void SavePosition()
        {
            SettingsManager.Settings.UiElementPositions[_draggableElementName] =
                _rectTransform.anchoredPosition;
        }
    }
}