using UnityEngine;

namespace YARG.Gameplay.Visuals
{
    public class NoteGroup : MonoBehaviour
    {
        private static readonly int _randomFloat = Shader.PropertyToID("_RandomFloat");
        private static readonly int _randomVector = Shader.PropertyToID("_RandomVector");

        [SerializeField]
        private MeshRenderer _meshRenderer;

        [SerializeField]
        private int _coloredMaterialIndex;

        public Material ColoredMaterial { get; private set; }

        private void Awake()
        {
            ColoredMaterial = _meshRenderer.materials[_coloredMaterialIndex];
        }

        public void InitializeRandomness()
        {
            if (ColoredMaterial.HasFloat(_randomFloat))
            {
                ColoredMaterial.SetFloat(_randomFloat, Random.Range(-1f, 1f));
            }

            if (ColoredMaterial.HasVector(_randomVector))
            {
                ColoredMaterial.SetVector(_randomVector, new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)));
            }
        }

        public Material[] GetAllMaterials() => _meshRenderer.materials;

        public void SetActive(bool a) => gameObject.SetActive(a);
    }
}