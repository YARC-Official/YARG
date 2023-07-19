using UnityEngine;
using YARG.Gameplay.Player;

namespace YARG.Gameplay.Visuals
{
    public class SustainLine : MonoBehaviour
    {
        private static readonly int _emissionColor = Shader.PropertyToID("_EmissionColor");

        [SerializeField]
        private LineRenderer _lineRenderer;

        private Material _material;

        private void Awake()
        {
            _material = _lineRenderer.materials[0];
        }

        public void SetInitialLength(float len)
        {
            // Make sure to make point 0 higher up so it renders it in the correct direction
            _lineRenderer.SetPosition(0, new(0f, 0.01f, len));
            _lineRenderer.SetPosition(1, Vector3.zero);
        }

        public void UpdateLengthForHit()
        {
            // Get the new line start position. Said position should be at
            // the strike line and relative to the note itself.
            float newStart = -transform.parent.localPosition.z + BasePlayer.STRIKE_LINE_POS;

            // Apply to line renderer
            _lineRenderer.SetPosition(1, new(0f, 0f, newStart));
        }

        public void SetColor(Color c)
        {
            _material.color = c;
            _material.SetColor(_emissionColor, c);
        }

        public void SetMissed()
        {
            _material.color = new(0.5f, 0.5f, 0.5f, 1f);
            _material.SetColor(_emissionColor, Color.black);
        }
    }
}