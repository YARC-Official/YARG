using UnityEngine;
using YARG.Gameplay.Player;
using YARG.Helpers.Extensions;

namespace YARG.Gameplay.Visuals
{
    public class SustainLine : MonoBehaviour
    {
        private const float GLOW_THRESHOLD = 0.15f;

        private static readonly int _emissionColor = Shader.PropertyToID("_EmissionColor");
        private static readonly int _glowAmount    = Shader.PropertyToID("_GlowAmount");

        private static readonly int _primaryAmplitude   = Shader.PropertyToID("_PrimaryAmplitude");
        private static readonly int _secondaryAmplitude = Shader.PropertyToID("_SecondaryAmplitude");
        private static readonly int _tertiaryAmplitude  = Shader.PropertyToID("_TertiaryAmplitude");
        private static readonly int _forwardOffset      = Shader.PropertyToID("_ForwardOffset");

        [SerializeField]
        private LineRenderer _lineRenderer;
        [SerializeField]
        private bool _setShaderProperties = true;

        private Material _material;
        private TrackPlayer _player;

        private SustainState _hitState = SustainState.Waiting;
        private float _whammyFactor;

        private float _secondaryAmplitudeTime;
        private float _tertiaryAmplitudeTime;

        private void Awake()
        {
            _player = GetComponentInParent<TrackPlayer>();
            _material = _lineRenderer.material;
        }

        private void Start()
        {
            float fadePos = _player.ZeroFadePosition;
            float fadeSize = _player.FadeSize;

            _material.SetFade(fadePos, fadeSize);
        }

        public void Initialize(float len)
        {
            // Set initial line length
            // Make sure to make point 0 higher up so it renders it in the correct direction
            _lineRenderer.SetPosition(0, new Vector3(0f, 0.01f, len));
            _lineRenderer.SetPosition(1, Vector3.zero);

            ResetAmplitudes();
        }

        public void SetState(SustainState state, Color c)
        {
            _hitState = state;

            // Get the glow value based on the value of the color
            Color.RGBToHSV(c, out _, out _, out float value);
            float glow = Mathf.Max((GLOW_THRESHOLD - value) / GLOW_THRESHOLD, 0f);

            switch (state)
            {
                case SustainState.Waiting:
                    _material.color = c;
                    _material.SetColor(_emissionColor, c);
                    _material.SetFloat(_glowAmount, glow * 0.9f);
                    break;
                case SustainState.Hitting:
                    _material.color = c;
                    _material.SetColor(_emissionColor, c * 3f);
                    _material.SetFloat(_glowAmount, glow);
                    break;
                case SustainState.Missed:
                    _material.color = new Color(0f, 0f, 0f, 1f);
                    _material.SetColor(_emissionColor, new Color(0.1f, 0.1f, 0.1f, 1f));
                    _material.SetFloat(_glowAmount, 0f);
                    ResetAmplitudes();
                    break;
            }
        }

        private void ResetAmplitudes()
        {
            if (!_setShaderProperties) return;

            _material.SetFloat(_primaryAmplitude, 0f);
            _material.SetFloat(_secondaryAmplitude, 0f);
            _material.SetFloat(_tertiaryAmplitude, 0f);

            _whammyFactor = 0f;

            _secondaryAmplitudeTime = 0f;
            _tertiaryAmplitudeTime = 0f;
        }

        public void UpdateSustainLine(float noteSpeed)
        {
            UpdateLengthForHit();
            UpdateAnimation(noteSpeed);
        }

        private void UpdateLengthForHit()
        {
            if (_hitState != SustainState.Hitting)
            {
                return;
            }

            // Get the new line start position. Said position should be at
            // the strike line and relative to the note itself.
            float newStart = -transform.parent.localPosition.z + TrackPlayer.STRIKE_LINE_POS;

            // Apply to line renderer
            _lineRenderer.SetPosition(1, new(0f, 0f, newStart));
        }

        private void UpdateAnimation(float noteSpeed)
        {
            // TODO: Reduce the amount of magic numbers lol

            if (!_setShaderProperties || _hitState != SustainState.Hitting)
            {
                return;
            }

            // Update whammy factor
            if (_player is FiveFretPlayer player)
            {
                // Make sure to lerp it to prevent jumps
                _whammyFactor = Mathf.Lerp(_whammyFactor, player.WhammyFactor, Time.deltaTime * 6f);
            }

            float whammy = _whammyFactor * 1.5f;

            // Update the amplitude times
            _secondaryAmplitudeTime += Time.deltaTime * (4f + whammy);
            _tertiaryAmplitudeTime += Time.deltaTime * (1.7f + whammy);

            // Change line amplitude
            _material.SetFloat(_primaryAmplitude, 0.18f + whammy * 0.2f);
            _material.SetFloat(_secondaryAmplitude, Mathf.Sin(_secondaryAmplitudeTime) * (whammy + 0.5f));
            _material.SetFloat(_tertiaryAmplitude, Mathf.Sin(_tertiaryAmplitudeTime) * (whammy * 0.1f + 0.1f));

            // Move line forward
            float forwardSub = Time.deltaTime * noteSpeed / 2.5f * (1f + whammy * 0.1f);
            _material.SetFloat(_forwardOffset, _material.GetFloat(_forwardOffset) + forwardSub);
        }
    }
}