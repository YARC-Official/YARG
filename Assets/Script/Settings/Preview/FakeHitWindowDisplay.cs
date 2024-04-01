using UnityEngine;
using YARG.Core.Engine;
using YARG.Helpers.Extensions;
using YARG.Settings.Customization;
using YARG.Settings.Metadata;

namespace YARG.Settings.Preview
{
    public class FakeHitWindowDisplay : MonoBehaviour
    {
        public HitWindowSettings HitWindow { get; set; }
        public float NoteSpeed { get; set; }

        private Transform _transformCache;

        private void Awake()
        {
            _transformCache = transform;

            // Set fade (required in case the hit window goes past the fade threshold)
            var cameraPreset = PresetsTab.GetLastSelectedPreset(CustomContentManager.CameraSettings);
            GetComponent<MeshRenderer>().material.SetFade(3f, cameraPreset.FadeLength);
        }

        private void Update()
        {
            // 1 will be max, 0 will be min. Do a sine wave between the two if it's dynamic
            var lerp = 1f;
            if (HitWindow.IsDynamic)
            {
                lerp = Mathf.Sin(Time.time * 2f) / 2f + 0.5f;
            }

            var totalWindow = Mathf.Lerp((float) HitWindow.MinWindow, (float) HitWindow.MaxWindow, lerp)
                * NoteSpeed;

            // Offsetting is done based on half of the size
            float baseOffset = (float) (-HitWindow.GetFrontEnd(totalWindow)
                - HitWindow.GetBackEnd(totalWindow)) / 2f;

            _transformCache.localScale = _transformCache.localScale
                .WithY(totalWindow);
            _transformCache.localPosition = _transformCache.localPosition
                .WithZ(baseOffset);
        }
    }
}