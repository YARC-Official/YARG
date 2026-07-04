using UnityEngine;

namespace YARG.Gameplay.Visuals
{
    public class StrikelineAnimator : MonoBehaviour
    {
        private Animator    _animator;

        [SerializeField]
        private ParticleSystem _leftParticles;
        [SerializeField]
        private ParticleSystem _rightParticles;

        private ParticleSystem.MinMaxGradient _rainbowGradient;
        private Color                         _openColor;

        private void Start()
        {
            _animator = GetComponent<Animator>();

            // Set up wildcard color
            var rainbowGradient1 = new Gradient();
            rainbowGradient1.SetKeys(new[] { new GradientColorKey(Color.purple, 0f), new GradientColorKey(Color.greenYellow, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            var rainbowGradient2 = new Gradient();
            rainbowGradient2.SetKeys(new[] { new GradientColorKey(Color.yellow, 0f), new GradientColorKey(Color.blue, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });

            _rainbowGradient = new ParticleSystem.MinMaxGradient(rainbowGradient1, rainbowGradient2);
            _rainbowGradient.mode = ParticleSystemGradientMode.TwoGradients;
        }

        public void SetSustaining(bool sustaining)
        {
            if (_animator == null)
            {
                return;
            }

            var animation = sustaining ? "Sustaining" : "Idle";
            _animator.Play(animation);

            if (sustaining)
            {
                _leftParticles.Play();
                _rightParticles.Play();
            }
            else
            {
                _leftParticles.Stop();
                _rightParticles.Stop();
            }
        }

        public void SetParticleColor(Color color)
        {
            var leftMain = _leftParticles.main;
            var rightMain = _rightParticles.main;
            leftMain.startColor = color;
            rightMain.startColor = color;
        }

        public void SetParticleRainbow()
        {
            var leftMain = _leftParticles.main;
            var rightMain = _rightParticles.main;
            leftMain.startColor = _rainbowGradient;
            rightMain.startColor = _rainbowGradient;
        }

    }
}