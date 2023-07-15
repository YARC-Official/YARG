using UnityEngine;

namespace YARG.Gameplay
{
    public class StarpowerBar : MonoBehaviour
    {
        private static readonly int _fill = Shader.PropertyToID("Fill");

        [SerializeField]
        private MeshRenderer _starpowerBar;

        public void SetStarpower(double starpowerAmount)
        {
            _starpowerBar.material.SetFloat(_fill, (float) starpowerAmount);
        }
    }
}