using TMPro;
using UnityEngine;
using YARG.Settings;

namespace YARG.Gameplay.Visuals
{
    public class ComboMeter : MonoBehaviour
    {
        private static readonly int SpriteIndexProperty = Shader.PropertyToID("_SpriteIndex");
        private static readonly int MultiplierColorProperty = Shader.PropertyToID("_MultiplierColor");
        private static readonly int GhostingColorProperty = Shader.PropertyToID("_GhostingColor");

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

        private void Start()
        {
            if (!SettingsManager.Settings.AntiGhosting.Value)
            {
                var ghostColor = _comboMesh.material.GetColor(GhostingColorProperty);
                _comboMesh.material.SetColor(MultiplierColorProperty, ghostColor);
            }
        }

        public void SetCombo(int multiplier, int maxMultiplier, int combo)
        {
            _multiplierText.text = multiplier != 1 ? $"{multiplier}<sub>x</sub>" : string.Empty;

            int index = combo % 10;
            if (multiplier != 1 && index == 0)
            {
                index = 10;
            }
            else if (multiplier == maxMultiplier)
            {
                index = 10;
            }

            _comboMesh.material.SetFloat(SpriteIndexProperty, index);
        }

        public void SetFullCombo(bool isFc)
        {
            _ringMesh.material = isFc ? _fcRingMaterial : _noFcRingMaterial;
        }
    }
}