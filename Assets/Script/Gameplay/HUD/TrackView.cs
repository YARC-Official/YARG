using UnityEngine;
using UnityEngine.UI;
using YARG.Core.Engine;
using YARG.Core.Game;
using YARG.Gameplay.Player;

namespace YARG.Gameplay.HUD
{
    public class TrackView : MonoBehaviour
    {
        private static readonly int _curveFactor = Shader.PropertyToID("_CurveFactor");

        [field: SerializeField]
        public RawImage TrackImage { get; private set; }

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

        private TrackPlayer _trackPlayer;

        private void Start()
        {
            _aspectRatioFitter.aspectRatio = (float) Screen.width / Screen.height;
        }

        public void Initialize(RenderTexture rt, CameraPreset cameraPreset, TrackPlayer trackPlayer)
        {
            TrackImage.texture = rt;

            // Clone the material since RawImages don't use instanced materials
            var newMaterial = new Material(TrackImage.material);
            newMaterial.SetFloat(_curveFactor, cameraPreset.CurveFactor);
            TrackImage.material = newMaterial;

            _trackPlayer = trackPlayer;
        }

        public void UpdateSizing(int trackCount)
        {
            // This equation calculates a good scale for all of the tracks.
            // It was made with experimentation; there's probably a "real" formula for this.
            float scale = Mathf.Max(0.7f * Mathf.Log10(trackCount - 1), 0f);
            scale = 1f - scale;

            TrackImage.transform.localScale = new Vector3(scale, scale, scale);
        }

        public void UpdateHUDPosition()
        {
            var rect = TrackImage.GetComponent<RectTransform>();
            var viewportPos = _trackPlayer.HUDViewportPosition;

            // Caching this is faster
            var rectRect = rect.rect;

            // Adjust the screen's viewport position to the rect's viewport position
            // TODO: I have no idea where this "- 0.5f" comes from. Are these calculations correct?
            var local = new Vector2(
                (viewportPos.x - 0.5f) * rectRect.width,
                viewportPos.y * rectRect.height);
            var screenPos = rect.TransformPoint(local);

            // Now, move the MoveContainer based on this
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform) _topElementContainer.parent,
                screenPos, null, out var localPoint);
            _topElementContainer.localPosition = localPoint;
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

        public void ForceReset()
        {
            _textNotifications.gameObject.SetActive(true);

            _soloBox.ForceReset();
            _textNotifications.ForceReset();
            _countdownDisplay.ForceReset();
        }
    }
}