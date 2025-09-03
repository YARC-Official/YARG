using UnityEngine;
using UnityEngine.Rendering;
using YARG.Gameplay.Player;

namespace YARG.Gameplay.Visuals
{
    [RequireComponent(typeof(LensFlareComponentSRP))]
    public class NoteFlare : MonoBehaviour
    {
        public TrackPlayer TrackPlayer { get; set; }

        private LensFlareComponentSRP _flare;

        private void Awake()
        {
            _flare = GetComponent<LensFlareComponentSRP>();
        }

        private void OnEnable()
        {
            _flare.intensity = 0f;
        }

        private void Update()
        {
            var pos = transform.position.z;
            float intensity = 1f;

            // We use "EaseInOutCubic" as the track fade also uses that
            _flare.intensity = EaseInOutCubic(Mathf.Clamp01(intensity));

            // Update flare scale based on distance from camera and attempt to normalize for FOV, includes tangent/cotangent for accurate FOV scaling

            float fovScale = (2.5f / Mathf.Tan(0.0125f * TrackPlayer.TrackCamera.fieldOfView - .05f)) + 1.282f;
            _flare.scale = fovScale / Vector3.Distance(transform.position, TrackPlayer.TrackCamera.transform.position);

        }

        private static float EaseInOutCubic(float x)
        {
            return x < 0.5f ? 4f * x * x * x : 1f - Mathf.Pow(-2f * x + 2f, 3f) / 2f;
        }
    }
}
