using Cysharp.Text;
using TMPro;
using UnityEngine;
using YARG.Core.Game;

namespace YARG.Gameplay.Visuals
{
    public class ComboMeter : MonoBehaviour
    {
        private static readonly int _spriteIndexProperty = Shader.PropertyToID("_SpriteIndex");
        private static readonly int _multiplierColorProperty = Shader.PropertyToID("_MultiplierColor");
        private static readonly int _customPresetColorProperty = Shader.PropertyToID("_CustomPresetColor");

        [SerializeField]
        private TextMeshPro _multiplierText;
        [SerializeField]
        private MeshRenderer _comboMesh;

        [Header("FC Ring")]
        [SerializeField]
        private MeshRenderer _ringMesh;

        [SerializeField]
        private Material _fcRingMaterial;
        [SerializeField]
        private Material _noFcRingMaterial;

        private TextMeshPro[] _textCache;

        public void Initialize(EnginePreset preset, int maxMultiplier)
        {
            _multiplierText.enabled = false;
            _multiplierText.text = string.Empty;
            _textCache = new TextMeshPro[maxMultiplier * 2 - 1];
            _textCache[0] = _multiplierText;
            for(int i = 0; i < _textCache.Length; ++i)
            {
                _textCache[i] = Instantiate(_multiplierText, _multiplierText.transform.parent, true);
                _textCache[i].SetTextFormat("{0}<sub>x</sub>", i + 2);
            }

            // Skip if the preset is a default one
            if (EnginePreset.Defaults.Contains(preset)) return;

            var color = _comboMesh.material.GetColor(_customPresetColorProperty);
            _comboMesh.material.SetColor(_multiplierColorProperty, color);
        }

        public void SetCombo(int multiplier, int maxMultiplier, int combo)
        {
            _multiplierText.enabled = false;
            if (multiplier > 1)
            {
                _multiplierText = _textCache[multiplier - 2];
                _multiplierText.enabled = true;
            }

            int index = combo % 10;
            if (combo != 0 && index == 0)
            {
                index = 10;
            }
            else if (multiplier == maxMultiplier)
            {
                index = 10;
            }

            _comboMesh.material.SetFloat(_spriteIndexProperty, index);
        }

        public void SetFullCombo(bool isFc)
        {
            _ringMesh.sharedMaterial = isFc ? _fcRingMaterial : _noFcRingMaterial;
        }
    }
}
