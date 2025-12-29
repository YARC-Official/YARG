using System;
using UnityEngine;
using YARG.Core.Engine;
using YARG.Gameplay.Visuals;
using YARG.Player;

namespace YARG.Gameplay.HUD
{
    public class TrackView : MonoBehaviour
    {
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

        private HighwayCameraRendering      _highwayRenderer;
        private Vector3 _lastTrackPlayerPosition;

        private const float CENTER_ELEMENT_DEPTH = 0.35f;

        public void Initialize(HighwayCameraRendering highwayRenderer)
        {
            _highwayRenderer = highwayRenderer;
        }

        public void UpdateHUDPosition(int highwayIndex, int highwayCount)
        {
            //Scale ui according to number of highways,
            //1 highway = 1.0 scale, 2 highways = 0.9 scale, 3 highways = 0.8 scale, etc, minimum of 0.5
            var newScale = Math.Max(0.5f, 1.1f - (0.1f * highwayCount));
            _scaleContainer.localScale = _scaleContainer.localScale.WithX(newScale).WithY(newScale);

            //Set center element position
            Vector2 position = _highwayRenderer.GetTrackPositionScreenSpace(highwayIndex, 0.5f, CENTER_ELEMENT_DEPTH);
            _centerElementContainer.transform.position = position;

            // Place top elements at 100% depth plus screen independent units up to avoid highway overlap
            var extraOffset = 8 * Screen.height / 1000f;
            Vector2 position2 = _highwayRenderer.GetTrackPositionScreenSpace(highwayIndex, 0.5f, 1.0f).AddY(extraOffset);
            _topElementContainer.position = position2;
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
    }
}
