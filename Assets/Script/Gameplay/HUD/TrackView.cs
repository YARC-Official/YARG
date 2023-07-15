using UnityEngine;
using UnityEngine.UI;

namespace YARG.Gameplay.HUD
{
    public class TrackView : MonoBehaviour
    {
        [field: SerializeField]
        public RawImage TrackImage { get; private set; }

        [SerializeField]
        private AspectRatioFitter _aspectRatioFitter;

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
    }
}