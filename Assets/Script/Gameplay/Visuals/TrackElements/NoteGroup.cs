using System.Linq;
using UnityEngine;
using YARG.Themes;

namespace YARG.Gameplay.Visuals
{
    public class NoteGroup : MonoBehaviour
    {
        private static readonly int _emissionColor = Shader.PropertyToID("_EmissionColor");

        private static readonly int _randomFloat = Shader.PropertyToID("_RandomFloat");
        private static readonly int _randomVector = Shader.PropertyToID("_RandomVector");

        // If we want info to be copied over when we copy the prefab,
        // we must make them SerializeFields.
        [SerializeField]
        private ThemeNote _themeNote;

        private Material[] _coloredMaterialCache;
        private Material[] _coloredMaterialNoStarPowerCache;
        private Material[] _allColoredCache;

        public void Initialize()
        {
            _coloredMaterialCache =
                _themeNote.GetColoredMaterials().ToArray();
            _coloredMaterialNoStarPowerCache =
                _themeNote.GetColoredMaterialsNoStarPower().ToArray();

            _allColoredCache = _coloredMaterialCache
                .Concat(_coloredMaterialNoStarPowerCache)
                .ToArray();

            // Set random values
            var randomFloat = Random.Range(-1f, 1f);
            var randomVector = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f));
            foreach (var material in _allColoredCache)
            {
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

        // TODO: Move `emissionMultiplier` into the ThemeNote
        public void SetColorWithEmission(Color color, Color colorNoStarPower,
            float emissionMultiplier = 8f)
        {
            // Deal with color (with star power)

            var realColor = color;
            if (_themeNote.AddExtraGlow)
            {
                realColor += new Color(3f, 3f, 3f);
            }

            foreach (var material in _coloredMaterialCache)
            {
                material.color = realColor;
                material.SetColor(_emissionColor, realColor * emissionMultiplier);
            }

            // Deal with color (no star power)
            if (_coloredMaterialNoStarPowerCache.Length == 0) return;

            var realColorNoStarPower = colorNoStarPower;
            if (_themeNote.AddExtraGlow)
            {
                realColorNoStarPower += new Color(3f, 3f, 3f);
            }

            foreach (var material in _coloredMaterialNoStarPowerCache)
            {
                material.color = realColorNoStarPower;
                material.SetColor(_emissionColor, realColorNoStarPower * emissionMultiplier);
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