using System.Linq;
using UnityEngine;
using YARG.Themes;

namespace YARG.Gameplay.Visuals
{
    public class NoteGroup : MonoBehaviour
    {
        private static readonly int _baseColor = Shader.PropertyToID("_Color");
        private static readonly int _emissionColor = Shader.PropertyToID("_EmissionColor");
        private static readonly int _randomFloat = Shader.PropertyToID("_RandomFloat");
        private static readonly int _randomVector = Shader.PropertyToID("_RandomVector");

        // If we want info to be copied over when we copy the prefab,
        // we must make them SerializeFields.
        [SerializeField]
        private ThemeNote _themeNote;

        private MeshEmissionMaterialIndex[] _coloredMaterialCache;
        private MeshEmissionMaterialIndex[] _coloredMaterialNoStarPowerCache;
        private MeshEmissionMaterialIndex[] _allColoredCache;

        public void Initialize()
        {
            _coloredMaterialCache ??= _themeNote.ColoredMaterials.ToArray();
            _coloredMaterialNoStarPowerCache ??= _themeNote.ColoredMaterialsNoStarPower.ToArray();
            _allColoredCache ??= _coloredMaterialCache.Concat(_coloredMaterialNoStarPowerCache).ToArray();

            // Set random values
            var randomFloat = Random.Range(-1f, 1f);
            var randomVector = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f));
            foreach (var info in _allColoredCache)
            {
                info.Mesh.GetPropertyBlock(MaterialPropertyInstance.Instance, info.MaterialIndex);

                if (info.Mesh.sharedMaterials[info.MaterialIndex].HasFloat(_randomFloat))
                {
                    MaterialPropertyInstance.Instance.SetFloat(_randomFloat, randomFloat);
                }

                if (info.Mesh.sharedMaterials[info.MaterialIndex].HasVector(_randomVector))
                {
                    MaterialPropertyInstance.Instance.SetVector(_randomVector, randomVector);
                }
                info.Mesh.SetPropertyBlock(MaterialPropertyInstance.Instance, info.MaterialIndex);

                RenderingOrder.FixUpMaterialRenderingOrder(info.Mesh);
            }
        }

        public void SetColorWithEmission(Color color, Color colorNoStarPower)
        {
            // Deal with color (with star power)

            foreach (var info in _coloredMaterialCache)
            {
                float a = info.EmissionAddition;
                var realColor = color + new Color(a, a, a);

                info.Mesh.GetPropertyBlock(MaterialPropertyInstance.Instance, info.MaterialIndex);
                MaterialPropertyInstance.Instance.SetColor(_baseColor, realColor);
                if (info.Mesh.sharedMaterials[info.MaterialIndex].HasColor(_emissionColor))
                {
                    MaterialPropertyInstance.Instance.SetColor(_emissionColor, realColor * info.EmissionMultiplier);
                }
                info.Mesh.SetPropertyBlock(MaterialPropertyInstance.Instance, info.MaterialIndex);
            }

            // Deal with color (no star power)
            if (_coloredMaterialNoStarPowerCache.Length == 0) return;

            foreach (var info in _coloredMaterialNoStarPowerCache)
            {
                float a = info.EmissionAddition;
                var realColor = colorNoStarPower + new Color(a, a, a);

                info.Mesh.GetPropertyBlock(MaterialPropertyInstance.Instance, info.MaterialIndex);
                MaterialPropertyInstance.Instance.SetColor(_baseColor, realColor);
                if (info.Mesh.sharedMaterials[info.MaterialIndex].HasColor(_emissionColor))
                {
                    MaterialPropertyInstance.Instance.SetColor(_emissionColor, realColor * info.EmissionMultiplier);
                }
                info.Mesh.SetPropertyBlock(MaterialPropertyInstance.Instance, info.MaterialIndex);
            }
        }

        public void SetActive(bool a) => gameObject.SetActive(a);

        /// <summary>
        /// This method is only called <b>once</b> when the note prefab is being created
        /// for this theme.
        /// </summary>
        public static NoteGroup CreateNoteGroupFromTheme(Transform parent, GameObject themeModel)
        {
            var noteObj = new GameObject("Note Group");
            var noteTransform = noteObj.transform;

            noteTransform.parent = parent;
            noteTransform.localPosition = Vector3.zero;

            var noteGroup = noteObj.AddComponent<NoteGroup>();
            noteGroup.SetModelFromTheme(themeModel);

            return noteGroup;
        }

        private void SetModelFromTheme(GameObject model)
        {
            // Copy the model
            var copy = Instantiate(model, transform);
            copy.transform.localPosition = Vector3.zero;

            // Set new information
            var themeNote = copy.GetComponent<ThemeNote>();
            _themeNote = themeNote;
        }
    }
}
