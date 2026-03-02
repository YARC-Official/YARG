using UnityEngine;
using YARG.Settings;

namespace YARG.Venue.VenueCamera
{
    public class LowFPSCamera : MonoBehaviour
    {
        public Camera TargetCamera;
        private float  _targetFPS;
        private float _timePassed;
        private float _interval;
        private bool _isRenderTextureCamera;

        void Start()
        {
            _targetFPS = SettingsManager.Settings.VenueFpsCap.Value;
            _interval = 1f / _targetFPS;
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
