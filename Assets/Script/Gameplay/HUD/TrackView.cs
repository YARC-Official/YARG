using UnityEditor.Localization.Plugins.XLIFF.V20;
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

        public void HitNote()
        {
            if (_soloBox.gameObject.activeSelf)
            {
                _soloBox.HitNote();
            }
        }

        public void StartSolo(SoloSection solo)
        {
            _soloBox.StartSolo(solo);
        }

        public void EndSolo(int soloBonus)
        {
            _soloBox.EndSolo(soloBonus);
        }
    }
}