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

            // Original values captured at From() time — used for proportional
            // scaling in OverrideZeroEmission so repeated calls don't compound.
            public float OriginalEmissionMultiplier;
            public float OriginalEmissionAddition;

            public bool OriginalEmissionEnabled;
            public bool OriginalEmissionDisabled;

            public static MaterialInfo From(MeshEmissionMaterialIndex a)
            {
                try
                {
                    var material = a.Mesh.materials[a.MaterialIndex];
                    return new MaterialInfo
                    {
                        MaterialCache = material,
                        EmissionMultiplier = a.EmissionMultiplier,
                        EmissionAddition = a.EmissionAddition,
                        OriginalEmissionMultiplier = a.EmissionMultiplier,
                        OriginalEmissionAddition = a.EmissionAddition,
                        OriginalEmissionEnabled = material.IsKeywordEnabled(EMISSION_ENABLED_KEYWORD),
                        OriginalEmissionDisabled = material.IsKeywordEnabled(EMISSION_DISABLED_KEYWORD),
                    };
                }
                catch (System.Exception x)
                {
                    throw x;
                }
            }
        }

        private static readonly int _emissionColor = Shader.PropertyToID("_EmissionColor");

        private const string EMISSION_ENABLED_KEYWORD = "_EMISSION_ENABLED";
        private const string EMISSION_DISABLED_KEYWORD = "_EMISSION_DISABLED";

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

        /// <summary>
        /// Overrides emission for colored materials that originally had zero
        /// emission (the "dark strip" materials on tap and ghost notes).
        /// Uses the ORIGINAL prefab values (captured at Initialize time) so the
        /// method is idempotent across repeated calls.
        ///
        /// At multiplier 0: original appearance preserved (no change).
        /// At multiplier 1: full emission, original darkening (EmissionAddition)
        /// scaled to zero. Values in between interpolate proportionally:
        /// newAddition = originalAddition × (1 − multiplier).
        /// </summary>
        public void OverrideZeroEmission(float multiplier)
        {
            ApplyZeroEmissionOverride(_coloredMaterialCache, multiplier);
            ApplyZeroEmissionOverride(_coloredMaterialNoStarPowerCache, multiplier);
        }

        private static void ApplyZeroEmissionOverride(MaterialInfo[] cache, float multiplier)
        {
            multiplier = Mathf.Clamp01(multiplier);

            for (int i = 0; i < cache.Length; i++)
            {
                ref var info = ref cache[i];

                if (info.OriginalEmissionMultiplier != 0f)
                    continue;

                if (multiplier == 0f)
                {
                    info.EmissionMultiplier = info.OriginalEmissionMultiplier;
                    info.EmissionAddition = info.OriginalEmissionAddition;

                    var originalKeywords = info.MaterialCache;
                    if (info.OriginalEmissionDisabled)
                    {
                        originalKeywords.EnableKeyword(EMISSION_DISABLED_KEYWORD);
                    }
                    else
                    {
                        originalKeywords.DisableKeyword(EMISSION_DISABLED_KEYWORD);
                    }

                    if (info.OriginalEmissionEnabled)
                    {
                        originalKeywords.EnableKeyword(EMISSION_ENABLED_KEYWORD);
                    }
                    else
                    {
                        originalKeywords.DisableKeyword(EMISSION_ENABLED_KEYWORD);
                    }

                    continue;
                }

                info.EmissionMultiplier = multiplier;
                info.EmissionAddition = info.OriginalEmissionAddition * (1f - multiplier);

                var mat = info.MaterialCache;
                mat.DisableKeyword(EMISSION_DISABLED_KEYWORD);
                mat.EnableKeyword(EMISSION_ENABLED_KEYWORD);
            }
        }

        /// <summary>
        /// Resets EmissionAddition to 0 for all colored materials that
        /// originally had a non-zero addition. Used for open HOPO notes where
        /// the prefab's EmissionAddition of 1 washes the note color to white.
        /// </summary>
        public void ResetEmissionAddition()
        {
            ResetEmissionAddition(_coloredMaterialCache);
            ResetEmissionAddition(_coloredMaterialNoStarPowerCache);
        }

        private static void ResetEmissionAddition(MaterialInfo[] cache)
        {
            for (int i = 0; i < cache.Length; i++)
            {
                if (cache[i].OriginalEmissionAddition != 0f)
                {
                    cache[i].EmissionAddition = 0f;
                }
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
