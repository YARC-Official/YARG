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

        private static readonly int _textureStrength = Shader.PropertyToID("_TextureStrength");

        // If we want info to be copied over when we copy the prefab,
        // we must make them SerializeFields.
        [SerializeField]
        private ThemeNote _themeNote;

        private MaterialInfo[] _coloredMaterialCache;
        private MaterialInfo[] _metalColoredMaterialCache;
        private MaterialInfo[] _coloredMaterialNoStarPowerCache;
        private MaterialInfo[] _allColoredCache;

        public void Initialize()
        {
            _coloredMaterialCache ??= _themeNote.ColoredMaterials.Select(MaterialInfo.From).ToArray();
            _metalColoredMaterialCache ??= _themeNote.MetalColoredMaterials.Select(MaterialInfo.From).ToArray();
            _coloredMaterialNoStarPowerCache ??= _themeNote.ColoredMaterialsNoStarPower.Select(MaterialInfo.From).ToArray();
            _allColoredCache ??= _coloredMaterialCache
                                .Concat(_metalColoredMaterialCache)
                                .Concat(_coloredMaterialNoStarPowerCache)
                                .ToArray();

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

        public void SetColorWithEmission(Color color, (Color color, float textureStrength) metalTuple, Color colorNoStarPower)
        {
            SetColorWithEmission(color, metalTuple.color, colorNoStarPower);
            SetTextureStrength(metalTuple.textureStrength, _metalColoredMaterialCache);
        }

        public void SetColorWithEmission(Color color, Color metalColor, Color colorNoStarPower)
        {
            ApplyColorToMaterialCache(color, _coloredMaterialCache);
            ApplyColorToMaterialCache(metalColor, _metalColoredMaterialCache);
            ApplyColorToMaterialCache(colorNoStarPower, _coloredMaterialNoStarPowerCache);
        }

        private void ApplyColorToMaterialCache(Color color, MaterialInfo[] cache)
        {
            if (cache.Length == 0)
            {
                return;
            }

            foreach (var info in cache)
            {
                float a = info.EmissionAddition;
                var realColor = color + new Color(a, a, a);

                info.MaterialCache.color = realColor;
                info.MaterialCache.SetColor(_emissionColor, realColor * info.EmissionMultiplier);
            }
        }

        private void SetTextureStrength(float strength, MaterialInfo[] cache)
        {
            if (cache.Length == 0)
            {
                return;
            }

            foreach (var info in cache)
            {
                info.MaterialCache.SetFloat(_textureStrength, strength);
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