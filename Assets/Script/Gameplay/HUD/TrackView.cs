using System;
using UnityEngine;
using YARG.Core.Engine;
using YARG.Core.Logging;
using YARG.Gameplay.Visuals;
using YARG.Helpers.Extensions;
using YARG.Player;

namespace YARG.Gameplay.HUD
{
    public class TrackView : MonoBehaviour
    {

        [SerializeField]
        private RectTransform _highwayEditContainer;
        [SerializeField]
        private RectTransform _topElementContainer;
        [SerializeField]
        private RectTransform _centerElementContainer;
        [SerializeField]
        private RectTransform _scaleContainer;

        [Space]
        [SerializeField]
        private SoloBox _soloBox;
        [SerializeField]
        private TextNotifications _textNotifications;
        [SerializeField]
        private CountdownDisplay _countdownDisplay;
        [SerializeField]
        private PlayerNameDisplay _playerNameDisplay;


        private HighwayCameraRendering _highwayRenderer;
        private Vector3 _lastTrackPlayerPosition;

        private const float CENTER_ELEMENT_DEPTH = 0.35f;
        private const float TOP_ELEMENT_EXTRA_OFFSET = 8f;

        private DraggableHudElement _topDraggable;
        private DraggableHudElement _highwayDraggable;
        private Canvas _highwayEditCanvas;
        private RectTransform _highwayEditParentRect;

        private readonly Vector3 _hiddenPosition = new(-10000f, -10000f, 0f);

        public void Initialize(HighwayCameraRendering highwayRenderer)
        {
            _highwayRenderer = highwayRenderer;
            _topDraggable = _topElementContainer.GetComponent<DraggableHudElement>();
            _highwayDraggable = _highwayEditContainer.GetComponent<DraggableHudElement>();
            _highwayEditCanvas = _highwayEditContainer.GetComponentInParent<Canvas>();
            _highwayEditParentRect = _highwayEditContainer.parent as RectTransform;
            _highwayDraggable.PositionChanged += OnHighwayDraggablePositionChanged;
        }

        public void UpdateHUDPosition(int highwayIndex, int highwayCount)
        {
            // Scale ui according to number of highways,
            // 1 highway = 1.0 scale, 2 highways = 0.9 scale, 3 highways = 0.8 scale, etc, minimum of 0.5
            var newScale = Math.Max(0.5f, 1.1f - (0.1f * highwayCount));
            _scaleContainer.localScale = _scaleContainer.localScale.WithX(newScale).WithY(newScale);
            UpdateTopHud(highwayIndex);
            UpdateCenterHud(highwayIndex);
            UpdateTrackPosition(highwayIndex);
        }

        private void UpdateTopHud(int highwayIndex)
        {
            if (_topDraggable.HasCustomPosition)
            {
                return;
            }

            // Place top elements at 100% depth of the track, plus some extra amount above the track.
            var extraOffset = TOP_ELEMENT_EXTRA_OFFSET * Screen.height / 1000f;
            var topPosition =
                _highwayRenderer.GetTrackPositionScreenSpace(highwayIndex, 0.5f, 1.0f)?.AddY(extraOffset)
                ?? _hiddenPosition;
            _topElementContainer.position = topPosition;
            _topDraggable.SetDefaultPosition(_topElementContainer.anchoredPosition);
        }

        private void UpdateCenterHud(int highwayIndex)
        {
            var trackPositionScreenSpace =
                _highwayRenderer.GetTrackPositionScreenSpace(highwayIndex, 0.5f, CENTER_ELEMENT_DEPTH);
            var centerPosition = trackPositionScreenSpace ?? _hiddenPosition;
            _centerElementContainer.transform.position = centerPosition;
        }

        // Keep the edit box sized to the track bounds and vertically centered to the track.
        private void UpdateTrackPosition(int highwayIndex)
        {
            SetHighwayOffsetX(_highwayDraggable.StoredPosition.x);

            var trackBounds = _highwayRenderer.GetTrackBoundsScreenSpace(highwayIndex);
            if (trackBounds == null)
            {
                _highwayEditContainer.position = _hiddenPosition;
                return;
            }

            //Set highway edit box size in canvas units
            float width = trackBounds.Value.width / _highwayEditCanvas.scaleFactor;
            float height = trackBounds.Value.height / _highwayEditCanvas.scaleFactor;
            _highwayEditContainer.sizeDelta = new Vector2(width, height);

            //Center the highway edit box on the highway
            var trackCenterScreenSpace = trackBounds.Value.center;
            var localCenter = _highwayEditParentRect.ScreenPointToLocalPoint(trackCenterScreenSpace);
            if (localCenter == null)
            {
                _highwayEditContainer.position = _hiddenPosition;
                return;
            }

            bool hasCustomPosition = _highwayDraggable.HasCustomPosition;
            float targetX = hasCustomPosition
                ? _highwayDraggable.StoredPosition.x
                : localCenter.Value.x;
            _highwayEditContainer.anchoredPosition = new Vector2(targetX, localCenter.Value.y);

            if (!hasCustomPosition)
            {
                //Highway position was not changed by user, this becomes default position for resetting
                _highwayDraggable.SetDefaultPosition(localCenter.Value);
            }
        }

        private void OnHighwayDraggablePositionChanged(Vector2 position)
        {
            UpdateTopHud(0);
            UpdateCenterHud(0);
            UpdateTrackPosition(0);
        }

        private void SetHighwayOffsetX(float xOffsetLocal)
        {
            float offsetPx = xOffsetLocal * _highwayEditCanvas.scaleFactor;
            _highwayRenderer.SetHorizontalOffsetPx(offsetPx);
        }

        public void UpdateCountdown(double countdownLength, double endTime)
        {
            _countdownDisplay.UpdateCountdown(countdownLength, endTime);
        }

        public void StartSolo(SoloSection solo)
        {
            _soloBox.StartSolo(solo);

            // No text notifications during the solo
            _textNotifications.SetActive(false);
        }

        public void EndSolo(int soloBonus)
        {
            _soloBox.EndSolo(soloBonus, () =>
            {
                // Show text notifications again
                _textNotifications.SetActive(true);
            });
        }

        public void UpdateNoteStreak(int streak)
        {
            _textNotifications.UpdateNoteStreak(streak);
        }

        public void ShowNewHighScore()
        {
            _textNotifications.ShowNewHighScore();
        }

        public void ShowFullCombo()
        {
            _textNotifications.ShowFullCombo();
        }

        public void ShowHotStart()
        {
            _textNotifications.ShowHotStart();
        }

        public void ShowBassGroove()
        {
            _textNotifications.ShowBassGroove();
        }

        public void ShowStarPowerReady()
        {
            _textNotifications.ShowStarPowerReady();
        }

        public void ShowStrongFinish()
        {
            _textNotifications.ShowStrongFinish();
        }

        public void ShowPlayerName(YargPlayer player)
        {
            _playerNameDisplay.ShowPlayer(player);
        }

        public void ForceReset()
        {
            _textNotifications.SetActive(true);

            _soloBox.ForceReset();
            _textNotifications.ForceReset();
            _countdownDisplay.ForceReset();
        }

        private void OnDestroy()
        {
            _highwayDraggable.PositionChanged -= OnHighwayDraggablePositionChanged;
        }
    }
}
