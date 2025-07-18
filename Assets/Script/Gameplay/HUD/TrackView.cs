using UnityEngine;
using UnityEngine.UI;
using YARG.Core.Engine;
using YARG.Gameplay.Player;
using YARG.Player;

namespace YARG.Gameplay.HUD
{
    public class TrackView : MonoBehaviour
    {
        [SerializeField]
        private AspectRatioFitter _aspectRatioFitter;
        [SerializeField]
        private RectTransform _topElementContainer;

        [Space]
        [SerializeField]
        private SoloBox _soloBox;
        [SerializeField]
        private TextNotifications _textNotifications;
        [SerializeField]
        private CountdownDisplay _countdownDisplay;
        [SerializeField]
        private PlayerNameDisplay _playerNameDisplay;

        private TrackPlayer _trackPlayer;

        private void Start()
        {
            _aspectRatioFitter.aspectRatio = (float) Screen.width / Screen.height;
        }

        public void Initialize(TrackPlayer trackPlayer)
        {
            _trackPlayer = trackPlayer;
        }

        public void UpdateHUDPosition(float scale)
        {
            var rect = GetComponent<RectTransform>();
            var viewportPos = _trackPlayer.HUDViewportPosition;

            // Caching this is faster
            var rectRect = rect.rect;

            // Adjust the screen's viewport position to the rect's viewport position
            // -0.5f as our position is relative to center, not the corner
            _topElementContainer.localPosition = _topElementContainer.localPosition.WithY(rect.rect.height * (viewportPos.y - 0.5f));
        }

        public void UpdateCountdown(int measuresLeft, double countdownLength, double endTime)
        {
            _countdownDisplay.UpdateCountdown(measuresLeft, countdownLength, endTime);
        }

        public void StartSolo(SoloSection solo)
        {
            _soloBox.StartSolo(solo);

            // No text notifications during the solo
            _textNotifications.gameObject.SetActive(false);
        }

        public void EndSolo(int soloBonus)
        {
            _soloBox.EndSolo(soloBonus, () =>
            {
                // Show text notifications again
                _textNotifications.gameObject.SetActive(true);
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
            _textNotifications.gameObject.SetActive(true);

            _soloBox.ForceReset();
            _textNotifications.ForceReset();
            _countdownDisplay.ForceReset();
        }
    }
}
