using TMPro;
using UnityEngine;

namespace YARG.Gameplay
{
    public class ComboMeter : MonoBehaviour
    {
        private static readonly int _spriteNum = Shader.PropertyToID("SpriteNum");

        [SerializeField]
        private TextMeshPro _multiplierText;
        [SerializeField]
        private MeshRenderer _comboMesh;

        public void SetCombo(int multiplier, int maxMultiplier, int combo)
        {
            _multiplierText.text = $"{multiplier}<sub>x</sub>";

            int index = combo % 10;
            if (multiplier != 1 && index == 0)
            {
                index = 10;
            }
            else if (multiplier == maxMultiplier)
            {
                index = 10;
            }

            _comboMesh.material.SetFloat(_spriteNum, index);
        }
    }
}