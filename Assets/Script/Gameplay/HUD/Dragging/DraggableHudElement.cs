using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using YARG.Helpers.Extensions;

namespace YARG.Gameplay.HUD
{
    [RequireComponent(typeof(RectTransform))]
    public class DraggableHudElement : GameplayBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler,
        IPointerDownHandler
    {
        [SerializeField]
        private string _draggableElementName;

        [Space]
        [SerializeField]
        private bool _horizontal = true;
        [SerializeField]
        private bool _vertical = true;
        [SerializeField]
        private bool _allowScaling;

        [Space]
        [SerializeField]
        private UnityEvent<bool> _onEditModeChanged;

        [Space]
        [SerializeField]
        private DraggingDisplay _draggingDisplayPrefab;

        private DraggableHudManager _manager;
        private RectTransform _rectTransform;

        private DraggingDisplay _draggingDisplay;

        private Vector2 _defaultPosition;

        private bool _isSelected;
        private bool _isDragging;
        private DragMode _dragMode;

        private ScaleDragHandler _scaleHandler;

        public bool HasCustomPosition =>
            enabled && _manager.PositionProfile.HasElementPosition(_draggableElementName);
        public Vector2 StoredPosition { get; private set; }
        public float StoredScale => _scaleHandler?.CurrentScale ?? 1f;
        public bool AllowScaling => _allowScaling;

        public event Action<Vector2> PositionChanged;
        public event Action<float> ScaleChanged;

        private enum DragMode
        {
            Position,
            Scale
        }

        protected override void GameplayAwake()
        {
            _manager = GetComponentInParent<DraggableHudManager>();
            _rectTransform = GetComponent<RectTransform>();
            _scaleHandler = new ScaleDragHandler(_rectTransform);
        }

        public void SetDefaultPosition(Vector2 position)
        {
            _defaultPosition = position;
        }

        protected override void OnSongStarted()
        {
            if (GameManager.Players.Count > 1)
            {
                enabled = false;
                return;
            }

            _defaultPosition = _rectTransform.anchoredPosition;

            var customPosition = _manager.PositionProfile.GetElementPosition(_draggableElementName);
            if (customPosition.HasValue)
            {
                StoredPosition = customPosition.Value;
                _rectTransform.anchoredPosition = StoredPosition;
            }
            else
            {
                StoredPosition = _defaultPosition;
            }
            PositionChanged?.Invoke(StoredPosition);

            if (_allowScaling)
            {
                var customScale = _manager.PositionProfile.GetElementScale(_draggableElementName) ?? 1f;
                _scaleHandler.Initialize(customScale);
                if (!Mathf.Approximately(customScale, StoredScale))
                {
                    _manager.PositionProfile.SaveElementScale(_draggableElementName, StoredScale);
                }
                ScaleChanged?.Invoke(StoredScale);
            }

            _draggingDisplay = Instantiate(_draggingDisplayPrefab, transform);
            _draggingDisplay.DraggableHud = this;

            _draggingDisplay.Hide();
            _draggingDisplay.gameObject.SetActive(false);
        }

        protected override void GameplayDestroy()
        {
            if (_manager != null)
            {
                _manager.RemoveDraggableElement(this);
            }
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
                if (_dragMode == DragMode.Position)
                {
                    SavePosition();
                }

                _dragMode = DragMode.Position;
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
            if (!_manager.EditMode || _isDragging || eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            _isDragging = true;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_isDragging || eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            if (_dragMode == DragMode.Scale)
            {
                if (_scaleHandler.UpdateScale(eventData))
                {
                    _manager.PositionProfile.SaveElementScale(_draggableElementName, StoredScale);
                    ScaleChanged?.Invoke(StoredScale);
                }
                return;
            }

            var parentRect = _rectTransform.parent as RectTransform;
            if (parentRect == null)
            {
                return;
            }

            var localPoint = parentRect.ScreenPointToLocalPoint(eventData.position, eventData.pressEventCamera);
            var prevLocalPoint = parentRect.ScreenPointToLocalPoint(
                eventData.position - eventData.delta, eventData.pressEventCamera);
            if (localPoint == null || prevLocalPoint == null)
            {
                return;
            }

            var localDelta = localPoint.Value - prevLocalPoint.Value;

            var position = _rectTransform.anchoredPosition;
            var previousPosition = position;

            if (_horizontal)
            {
                position.x += localDelta.x;
            }

            if (_vertical)
            {
                position.y += localDelta.y;
            }

            if (position != previousPosition)
            {
                _rectTransform.anchoredPosition = position;
                SavePosition();
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_isDragging || eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            _isDragging = false;
            if (_dragMode == DragMode.Position)
            {
                SavePosition();
            }

            _dragMode = DragMode.Position;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!_manager.EditMode || eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            if (!_isSelected)
            {
                _manager.SetSelectedElement(this);
            }

            if (_allowScaling && _scaleHandler.ShouldScale(eventData, _draggingDisplay.ScaleHandle))
            {
                _dragMode = DragMode.Scale;
                _scaleHandler.BeginScaleDrag(eventData, _draggingDisplay.ScaleHandle);
            }
            else
            {
                _dragMode = DragMode.Position;
            }
        }

        public void RevertElement()
        {
            _rectTransform.anchoredPosition = StoredPosition;
            SavePosition();
        }

        public void ResetElement()
        {
            _rectTransform.anchoredPosition = _defaultPosition;
            StoredPosition = _defaultPosition;
            _manager.PositionProfile.RemoveElementPosition(_draggableElementName);
            PositionChanged?.Invoke(StoredPosition);

            _scaleHandler.Reset();
            _manager.PositionProfile.RemoveElementScale(_draggableElementName);
            ScaleChanged?.Invoke(StoredScale);
        }

        private void SavePosition()
        {
            StoredPosition = _rectTransform.anchoredPosition;
            _manager.PositionProfile.SaveElementPosition(_draggableElementName,
                _rectTransform.anchoredPosition);
            PositionChanged?.Invoke(StoredPosition);
        }
    }
}
