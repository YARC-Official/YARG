// pattern: Imperative Shell
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace YARG.Gameplay.HUD
{
    public sealed class HarmonyMeterGroup : MonoBehaviour
    {
        [SerializeField] private HarmonyMeter _meterPrefab;
        [SerializeField] private RectTransform _container;
        [SerializeField] private float _rowSpacing = 16f;

        private readonly List<HarmonyMeter> _meters = new();

        public void Initialize(IReadOnlyList<bool> availableParts, IReadOnlyList<Color> colors)
        {
            foreach (var meter in _meters)
            {
                if (meter == null) continue;
                meter.gameObject.SetActive(false);
                Destroy(meter.gameObject);
            }
            _meters.Clear();

            for (int part = 0; part < availableParts.Count; part++)
            {
                if (!availableParts[part])
                {
                    _meters.Add(null);
                    continue;
                }
                var meter = Instantiate(_meterPrefab, _container);
                meter.name = $"Harmony {part + 1}";
                meter.SetColor(part < colors.Count ? colors[part] : Color.white);
                meter.ResetVisuals();
                _meters.Add(meter);
            }

            // Keep lookup indexed by part; only the visual rows use high/lead/low order.
            int row = 0;
            for (int slot = 0; slot < _meters.Count; slot++)
            {
                int part = _meters.Count > 1 && slot < 2 ? 1 - slot : slot;
                var meter = _meters[part];
                if (meter == null) continue;
                ((RectTransform) meter.transform).anchoredPosition = new Vector2(0, -row * _rowSpacing);
                row++;
            }
        }

        public void PositionFeedbackBelow(RectTransform feedback, Vector3 originalPosition)
        {
            float bottom = float.PositiveInfinity;
            foreach (var meter in _meters)
            {
                if (meter == null) continue;
                bottom = Mathf.Min(bottom, meter.GetVisualBottom(feedback.parent));
            }
            // Leave room for the existing notification animation's 1.1x peak scale.
            float topExtent = feedback.GetComponentInChildren<TMP_Text>(true).fontSize * 0.5f * 1.1f;
            originalPosition.y = Mathf.Min(originalPosition.y, bottom - topExtent - 6f);
            feedback.localPosition = originalPosition;
        }

        public void SetPartColor(int partIndex, Color color)
        {
            if (partIndex >= 0 && partIndex < _meters.Count)
                _meters[partIndex]?.SetColor(color);
        }

        public void RenderPart(int partIndex, HarmonyMeterState state, bool fullCombo, float deltaTime)
        {
            if (partIndex >= 0 && partIndex < _meters.Count)
                _meters[partIndex]?.Render(state, fullCombo, deltaTime);
        }

        public void ResetVisuals()
        {
            foreach (var meter in _meters)
                if (meter != null) meter.ResetVisuals();
        }
    }
}