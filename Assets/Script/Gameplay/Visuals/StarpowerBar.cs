using UnityEngine;

namespace YARG.Gameplay.Visuals
{
    public class StarpowerBar : MonoBehaviour
    {
        private static readonly int _fill = Shader.PropertyToID("Fill");
        private static readonly int _pulse = Shader.PropertyToID("Pulse");

        [SerializeField]
        private MeshRenderer _starpowerBar;

        public void SetStarpower(double starpowerAmount)
        {
            _starpowerBar.material.SetFloat(_fill, (float) starpowerAmount);
        }

        public void PulseBar()
        {
            _starpowerBar.material.SetFloat(_pulse, 1f);
        }

        private void Update()
        {
            var mat = _starpowerBar.material;

            // Fade out the pulse
            float currentPulse = mat.GetFloat(_pulse);
            mat.SetFloat(_pulse, Mathf.Clamp01(currentPulse - Time.deltaTime * 6f));
        }
    }
}