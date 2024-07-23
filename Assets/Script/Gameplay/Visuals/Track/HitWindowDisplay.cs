using UnityEngine;
using YARG.Gameplay.Player;
using YARG.Helpers.Extensions;
using YARG.Rendering;

namespace YARG.Gameplay.Visuals.Track
{
    [RequireComponent(typeof(MeshRenderer))]
    public class HitWindowDisplay : MonoBehaviour
    {
        private Transform _transformCache;
        private RendererPropertyWrapper _properties;

        private void Awake()
        {
            _transformCache = transform;
            _properties = new(GetComponent<MeshRenderer>());
        }

        public void Initialize(float fadeZero, float fadeSize)
        {
            // Set fade (required in case the hit window goes past the fade threshold)
            _properties.SetFade(fadeZero, fadeSize);
        }

        public void SetSize(GameManager manager, TrackPlayer player)
        {
            var (frontEnd, backEnd) = player.BaseEngine.CalculateHitWindow();
            float speed = player.NoteSpeed;
            double offset = manager.VideoCalibration + player.InputCalibration;
            SetSize(-frontEnd, backEnd, speed, offset);
        }

        public void SetSize(double frontEndSize, double backEndSize, float speed, double offset)
        {
            double totalWindow = frontEndSize + backEndSize;

            // Offsetting is done based on half of the size
            double baseOffset = (frontEndSize - backEndSize) / 2;

            _transformCache.localScale = _transformCache.localScale
                .WithY((float) totalWindow * speed);
            _transformCache.localPosition = _transformCache.localPosition
                .WithZ((float) ((baseOffset + offset) * speed));
        }
    }
}