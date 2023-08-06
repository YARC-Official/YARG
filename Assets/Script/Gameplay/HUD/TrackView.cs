using UnityEngine;
using UnityEngine.UI;
using YARG.Core.Engine;

namespace YARG.Gameplay.HUD
{
    public class TrackView : MonoBehaviour
    {
        [field: SerializeField]
        public RawImage TrackImage { get; private set; }

        [SerializeField]
        private AspectRatioFitter _aspectRatioFitter;

        [Space]
        [SerializeField]
        private SoloBox _soloBox;
        [SerializeField]
        private TextNotifications _textNotifications;

        private void Start()
        {
            _aspectRatioFitter.aspectRatio = (float) Screen.width / Screen.height;
        }

        public void UpdateSizing(int trackCount)
        {
            float scale = Mathf.Max(0.7f * Mathf.Log10(trackCount - 1), 0f);
            scale = 1f - scale;

            TrackImage.transform.localScale = new Vector3(scale, scale, scale);
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
    }
}