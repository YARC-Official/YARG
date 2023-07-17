using UnityEngine;

namespace YARG.Gameplay
{
    public class NoteGroup : MonoBehaviour
    {
        [SerializeField]
        private MeshRenderer _meshRenderer;

        [SerializeField]
        private int _coloredMaterialIndex;

        public Material ColoredMaterial { get; private set; }

        private void Awake()
        {
            ColoredMaterial = _meshRenderer.materials[_coloredMaterialIndex];
        }

        public void SetActive(bool a) => gameObject.SetActive(a);
    }
}