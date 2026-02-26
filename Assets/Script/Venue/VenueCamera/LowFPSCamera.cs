using UnityEngine;

namespace YARG.Venue.VenueCamera
{
    public class LowFPSCamera : MonoBehaviour
    {
        public Camera TargetCamera;
        public float  TargetFPS = 30f;

        private float _timePassed;
        private float _interval;
        private bool _isRenderTextureCamera;

        void Start()
        {
            _interval = 1f / TargetFPS;
            //disable camera so it doesn't render on its own
            TargetCamera.enabled = false;
        }

        void Update()
        {
            _timePassed += Time.deltaTime;

            if (_timePassed >= _interval)
            {
                _timePassed -= _interval;
                TargetCamera.Render();
            }
        }
    }
}
