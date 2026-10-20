// pattern: Imperative Shell
using UnityEngine;
using UnityEngine.UI;

namespace YARG.Gameplay.HUD
{
    public sealed class HarmonyMeter : MonoBehaviour
    {
        [SerializeField] private Image _background;
        [SerializeField] private Image _fill;
        [SerializeField] private Image _rim;
        [SerializeField] private Color _partColor = Color.cyan;
        [SerializeField] private Color _inactiveColor = new(0.28f, 0.28f, 0.28f, 1f);
        [SerializeField] private Color _fullComboColor = new(1f, 0.84f, 0f, 1f);

        // The existing overlay artwork has 37 transparent rows at each end of its 122px height.
        [SerializeField, Range(0f, 0.5f)] private float _verticalSpritePadding = 37f / 122f;

        private bool _wasActive;
        private HarmonyMeterState _state;
        private bool _fullCombo;

        public float GetVisualBottom(Transform relativeTo)
        {
            var rect = _background.rectTransform.rect;
            float height = rect.height;
            if (_background.preserveAspect && _background.sprite != null)
            {
                var size = _background.sprite.rect.size;
                height = Mathf.Min(height, rect.width * size.y / size.x);
            }
            var bottom = rect.center - new Vector2(0, height * (0.5f - _verticalSpritePadding));
            return relativeTo.InverseTransformPoint(_background.rectTransform.TransformPoint(bottom)).y;
        }

        public void SetColor(Color color)
        {
            _partColor = color;
            ApplyColors();
        }

        public void Render(HarmonyMeterState state, bool fullCombo, float deltaTime)
        {
            _fill.fillAmount = HarmonyMeterPresentation.AdvanceFill(
                _fill.fillAmount, _wasActive, state, deltaTime);
            _state = state;
            _wasActive = state.Active;
            _fullCombo = fullCombo;
            ApplyColors();
        }

        public void ResetVisuals()
        {
            _wasActive = false;
            _state = default;
            _fill.fillAmount = 0;
            ApplyColors();
        }

        private void ApplyColors()
        {
            _background.color = _state.Active ? Color.black : _inactiveColor;
            _fill.enabled = _state.Active || _state.Warning;
            if (_state.Warning)
            {
                // Retain the configured hue while the beat peak approaches white.
                var flashColor = Color.Lerp(_partColor, Color.white, 0.65f);
                _fill.color = Color.Lerp(_inactiveColor, flashColor, _state.Pulse);
            }
            else
            {
                _fill.color = _partColor;
            }
            _rim.enabled = _state.Active;
            // This is the player's FC status, not a per-part FC claim.
            _rim.color = _fullCombo ? _fullComboColor : Color.white;
        }
    }
}