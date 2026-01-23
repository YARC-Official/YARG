using System.Linq;
using UnityEngine;
using YARG.Themes;

namespace YARG.Gameplay.Visuals
{
    public class NoteGroup : MonoBehaviour
    {
        private struct MaterialInfo
        {
            public Material MaterialCache;

            public float EmissionMultiplier;
            public float EmissionAddition;

            public static MaterialInfo From(MeshEmissionMaterialIndex a)
            {
                return new MaterialInfo
                {
                    MaterialCache      = a.Mesh.materials[a.MaterialIndex],
                    EmissionMultiplier = a.EmissionMultiplier,
                    EmissionAddition   = a.EmissionAddition,
                };
            }
        }

        private static readonly int _emissionColor = Shader.PropertyToID("_EmissionColor");

        private static readonly int _randomFloat = Shader.PropertyToID("_RandomFloat");
        private static readonly int _randomVector = Shader.PropertyToID("_RandomVector");

        // If we want info to be copied over when we copy the prefab,
        // we must make them SerializeFields.
        [SerializeField]
        private ThemeNote _themeNote;

        private MaterialInfo[] _coloredMaterialCache;
        private MaterialInfo[] _coloredMaterialNoStarPowerCache;
        private MaterialInfo[] _allColoredCache;
        private MaterialInfo[] _coloredMetalMaterialCache;

        public void Initialize()
        {
            _coloredMaterialCache ??= _themeNote.ColoredMaterials.Select(MaterialInfo.From).ToArray();
            _coloredMaterialNoStarPowerCache ??= _themeNote.ColoredMaterialsNoStarPower.Select(MaterialInfo.From).ToArray();
            _allColoredCache ??= _coloredMaterialCache.Concat(_coloredMaterialNoStarPowerCache).ToArray();
            _coloredMetalMaterialCache ??= _themeNote.ColoredMetalMaterials.Select(MaterialInfo.From).ToArray();

            // Set random values
            var randomFloat = Random.Range(-1f, 1f);
            var randomVector = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f));
            foreach (var info in _allColoredCache)
            {
                var material = info.MaterialCache;

                if (material.HasFloat(_randomFloat))
                {
                    material.SetFloat(_randomFloat, randomFloat);
                }

                if (material.HasVector(_randomVector))
                {
                    material.SetVector(_randomVector, randomVector);
                }
            }
        }

        public void SetColorWithEmission(Color color, Color colorNoStarPower)
        {
            // Deal with color (with star power)

            foreach (var info in _coloredMaterialCache)
            {
                float a = info.EmissionAddition;
                var realColor = color + new Color(a, a, a);

                info.MaterialCache.color = realColor;
                info.MaterialCache.SetColor(_emissionColor, realColor * info.EmissionMultiplier);
            }

            // Deal with color (no star power)
            if (_coloredMaterialNoStarPowerCache.Length == 0) return;

            foreach (var info in _coloredMaterialNoStarPowerCache)
            {
                float a = info.EmissionAddition;
                var realColor = colorNoStarPower + new Color(a, a, a);

                info.MaterialCache.color = realColor;
                info.MaterialCache.SetColor(_emissionColor, realColor * info.EmissionMultiplier);
            }
        }

        public void SetMetalColor(Color metalColor)
        {
            if (_coloredMetalMaterialCache.Length == 0) return;

            foreach (var info in _coloredMetalMaterialCache)
            {
                info.MaterialCache.color = metalColor;
                info.MaterialCache.SetColor(_emissionColor, metalColor);
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