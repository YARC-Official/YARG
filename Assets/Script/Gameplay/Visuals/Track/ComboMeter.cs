using Cysharp.Text;
using TMPro;
using UnityEngine;
using YARG.Core.Game;
using YARG.Rendering;

namespace YARG.Gameplay.Visuals.Track
{
    public class ComboMeter : MonoBehaviour
    {
        private static readonly int _spriteIndexProperty = Shader.PropertyToID("_SpriteIndex");
        private static readonly int _multiplierColorProperty = Shader.PropertyToID("_MultiplierColor");

        [Header("Combo")]
        [SerializeField]
        private TextMeshPro _multiplierText;
        [SerializeField]
        private MeshRenderer _comboMesh;
        [SerializeField]
        private Color _comboColor;
        [SerializeField]
        private Color _comboCustomPresetColor;

        [Header("FC Ring")]
        [SerializeField]
        private MeshRenderer _ringMesh;
        [SerializeField]
        private Material _fcRingMaterial;
        [SerializeField]
        private Material _noFcRingMaterial;

        private RendererPropertyWrapper _comboProperties;

        private void Awake()
        {
            _comboProperties = new(_comboMesh);
        }

        public void Initialize(EnginePreset preset)
        {
            // Recolor the combo meter if a custom preset is being used
            if (!EnginePreset.Defaults.Contains(preset))
            {
                _comboProperties.SetColor(_multiplierColorProperty, _comboCustomPresetColor);
            }
        }

        public void SetCombo(int multiplier, int maxMultiplier, int combo)
        {
            if (multiplier != 1)
                _multiplierText.SetTextFormat("{0}<sub>x</sub>", multiplier);
            else
                _multiplierText.text = string.Empty;

            int index = combo % 10;
            if (combo != 0 && index == 0)
            {
                index = 10;
            }
            else if (multiplier == maxMultiplier)
            {
                index = 10;
            }

            _comboProperties.SetFloat(_spriteIndexProperty, index);
        }

        public void SetFullCombo(bool isFc)
        {
            _ringMesh.material = isFc ? _fcRingMaterial : _noFcRingMaterial;
        }
    }
}