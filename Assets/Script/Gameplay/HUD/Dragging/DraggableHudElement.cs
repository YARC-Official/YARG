using System;
using TMPro;
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

        // The position to restore to when resetting.  Defaults to anchored position but can be set explicitly too
        private Vector2 _defaultPosition;

        private bool _isSelected;
        private DragMode _dragMode;

        // 1f = unscaled, prevents shrinking below default size
        private const float MIN_SCALE = 1f;

        private ScaleDragHandler _scaleHandler;

        private HUDPositionProfile PositionProfile => _manager.PositionProfile;

        private Vector2? PersistedPosition => PositionProfile.GetElementPosition(_draggableElementName);
        private float?   PersistedScale    => PositionProfile.GetElementScale(_draggableElementName);
        public bool HasCustomPosition => enabled && PositionProfile.HasElementPosition(_draggableElementName);
        public float CurrentScale => _scaleHandler?.CurrentScale ?? MIN_SCALE;
        public bool AllowScaling => _allowScaling;

        public RectTransform RectTransform => _rectTransform;
        public Vector2 CurrentPosition => _rectTransform.anchoredPosition;
        public event Action<Vector2> PositionChanged;
        public event Action<float> ScaleChanged;

        private enum DragMode
        {
            NONE,
            POSITION,
            SCALE
        }

        protected override void GameplayAwake()
        {
            _manager = GetComponentInParent<DraggableHudManager>();
            _rectTransform = GetComponent<RectTransform>();
            _scaleHandler = new ScaleDragHandler(_rectTransform, MIN_SCALE);
        }

        /// <summary>
        /// Overrides the default position used when resetting.
        /// If not called, the anchored position at song start is used.
        /// </summary>
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

            var customPosition = PersistedPosition;
            if (customPosition.HasValue)
            {
                _rectTransform.anchoredPosition = customPosition.Value;
            }
            PositionChanged?.Invoke(CurrentPosition);

            if (_allowScaling)
            {
                var customScale = PersistedScale ?? MIN_SCALE;
                _scaleHandler.Initialize(customScale);
                ScaleChanged?.Invoke(CurrentScale);
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
            SaveDragState();
            _dragMode = DragMode.NONE;
            _draggingDisplay.Hide();
        }

        public void OnEditModeChanged(bool on)
        {
            _draggingDisplay.gameObject.SetActive(on);
            _onEditModeChanged.Invoke(on);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!_manager.EditMode || !eventData.IsLeftButton())
            {
                return;
            }

            _manager.HandleBeginDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_manager.EditMode || !eventData.IsLeftButton())
            {
                return;
            }

            _manager.HandleDrag(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_manager.EditMode || !eventData.IsLeftButton())
            {
                return;
            }

            _manager.HandleEndDrag(eventData);
        }

        public void BeginDrag(PointerEventData eventData)
        {
            if (_dragMode != DragMode.NONE)
            {
                return;
            }

            var shouldScale = _allowScaling && _scaleHandler.ShouldScale(eventData, _draggingDisplay.ScaleHandle);
            if (shouldScale)
            {
                _dragMode = DragMode.SCALE;
                _scaleHandler.BeginScaleDrag(eventData, _draggingDisplay.ScaleHandle);
            }
            else
            {
                _dragMode = DragMode.POSITION;
            }
        }

        public void Drag(PointerEventData eventData)
        {
            if (_dragMode == DragMode.SCALE)
            {
                if (_scaleHandler.UpdateScale(eventData))
                {
                    ScaleChanged?.Invoke(CurrentScale);
                }
                return;
            }

            UpdatePosition(eventData);
        }

        public void EndDrag(PointerEventData eventData)
        {
            SaveDragState();
            _dragMode = DragMode.NONE;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!_manager.EditMode || !eventData.IsLeftButton())
            {
                return;
            }
            _manager.HandlePointerDown(eventData);
        }

        public bool IsScaleHandleAtPoint(Vector2 screenPoint, Camera camera)
        {
            return _isSelected && _draggingDisplay != null &&
                _draggingDisplay.IsScaleHandleAtPoint(screenPoint, camera);
        }


        public void ResetElement()
        {
            _rectTransform.anchoredPosition = _defaultPosition;
            PositionProfile.RemoveElementPosition(_draggableElementName);
            NotifyPositionChanged();

            _scaleHandler.Reset();
            PositionProfile.RemoveElementScale(_draggableElementName);
            NotifyScaleChanged();
        }

        private void UpdatePosition(PointerEventData eventData)
        {
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
                NotifyPositionChanged();
            }
        }

        private void SaveDragState()
        {
            if (_dragMode == DragMode.POSITION)
            {
                PositionProfile.SaveElementPosition(_draggableElementName, _rectTransform.anchoredPosition);
            }
            else if (_dragMode == DragMode.SCALE)
            {
                PositionProfile.SaveElementScale(_draggableElementName, CurrentScale);
            }
        }

        private void NotifyPositionChanged()
        {
            PositionChanged?.Invoke(CurrentPosition);
        }

        private void NotifyScaleChanged()
        {
            ScaleChanged?.Invoke(CurrentScale);
        }
    }
}
