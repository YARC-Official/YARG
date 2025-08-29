using UnityEngine;

namespace YARG.Gameplay.Visuals
{
    public class StarpowerBar : MonoBehaviour
    {
        private static readonly int _fill = Shader.PropertyToID("Fill");
        private static readonly int _pulse = Shader.PropertyToID("Pulse");

        [SerializeField]
        private MeshRenderer _starpowerBar;

        private double _starpowerAmount;
        private bool _starpowerActive;

        public void SetStarpower(double starpowerAmount, bool starpowerActive)
        {
            _starpowerAmount = starpowerAmount;
            _starpowerActive = starpowerActive;

            _starpowerBar.material.SetFloat(_fill, (float) starpowerAmount);
        }

        public void UpdateFlash(double beatPercentage)
        {
            if (_starpowerAmount >= 0.5 || _starpowerActive)
            {
                float pulse = 1 - (float) beatPercentage;
                float relativePulse = GetRelativePulse(pulse);
                _starpowerBar.material.SetFloat(_pulse, relativePulse);
            }
            else
            {
                _starpowerBar.material.SetFloat(_pulse, 0f);
            }
        }

        private float GetRelativePulse(float pulse)
        {
            // Use x^2 curve to make the flash more noticeable
            return pulse * pulse;
        }
    }
}