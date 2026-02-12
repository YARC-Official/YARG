using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using YARG.Core.Logging;

namespace YARG.Menu.Persistent
{
    public class MenuFontScaler : MonoBehaviour
    {
        [SerializeField]
        private float _minimumFontSize = 14f;

        private TMP_Text[] _allTexts;

        private struct Baseline
        {
            public float FontSize;
            public float FontSizeMin;
            public float FontSizeMax;
        }

        private readonly Dictionary<int, Baseline> _baselines = new();
        private void Start()
        {
            _allTexts = GetAllTextObjects();
            CacheBaselines();
            ScaleText();

            YargLogger.LogDebug(">>Found " + _allTexts.Length + " text objects for scaling.");

            // PrintAllTextSizes();
        }

        private void PrintAllTextSizes()
        {
            foreach (var text in _allTexts)
            {
                int id = text.GetInstanceID();
                var baseline = _baselines[id];

                if (text.enableAutoSizing)
                {
                    YargLogger.LogFormatDebug("  {0}: auto [{1}-{2}] -> [{3}-{4}]",
                        text.name, baseline.FontSizeMin, baseline.FontSizeMax,
                        text.fontSizeMin, text.fontSizeMax);
                }
                else
                {
                    YargLogger.LogFormatDebug("  {0}: {1} -> {2}",
                        text.name, baseline.FontSize, text.fontSize);
                }
            }
        }

        private void ScaleText()
        {
            foreach (var text in _allTexts)
            {
                int id = text.GetInstanceID();
                var baseline = _baselines[id];

                if (text.enableAutoSizing)
                {
                    float newMin = Mathf.Max(baseline.FontSizeMin, _minimumFontSize);
                    float newMax = Mathf.Max(baseline.FontSizeMax, _minimumFontSize);
                    if (newMin != baseline.FontSizeMin || newMax != baseline.FontSizeMax)
                    {
                        text.fontSizeMin = newMin;
                        text.fontSizeMax = newMax;
                        YargLogger.LogFormatDebug("Scaled {0}: auto [{1}-{2}] -> [{3}-{4}]",
                            text.name, baseline.FontSizeMin, baseline.FontSizeMax,
                            newMin, newMax);
                    }
                }
                else
                {
                    float newSize = Mathf.Max(baseline.FontSize, _minimumFontSize);
                    if (newSize != baseline.FontSize)
                    {
                        text.fontSize = newSize;
                        YargLogger.LogFormatDebug("Scaled {0}: {1} -> {2}",
                            text.name, baseline.FontSize, newSize);
                    }
                }
            }
        }

        private void CacheBaselines()
        {
            foreach (var text in _allTexts)
            {
                int id = text.GetInstanceID();
                _baselines[id] = new Baseline
                {
                    FontSize = text.fontSize,
                    FontSizeMin = text.fontSizeMin,
                    FontSizeMax = text.fontSizeMax,
                };
            }
        }

        private TMP_Text[] GetAllTextObjects()
        {
            YargLogger.LogDebug(">>GETTING all text objects");
            return GetComponentsInChildren<TMP_Text>(true);
        }

        // Re-apply scaling when values are changed in the inspector
        private void OnValidate()
        {
            if (_allTexts == null || _allTexts.Length == 0)
            {
                return;
            }

            ScaleText();
        }
    }
}
