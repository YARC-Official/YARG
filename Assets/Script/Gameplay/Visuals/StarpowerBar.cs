using UnityEngine;

namespace YARG.Gameplay.Visuals
{
    public class StarpowerBar : GameplayBehaviour
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

        private void Update()
        {
            if (_starpowerAmount >= 0.5 || _starpowerActive)
            {
                float pulse = 1 - (float) GameManager.BeatEventHandler.Visual.StrongBeat.CurrentPercentage;
                _starpowerBar.material.SetFloat(_pulse, pulse);
            }
            else
            {
                _starpowerBar.material.SetFloat(_pulse, 0f);
            }
        }
    }
}