using UnityEngine;
using YARG.Core.Chart;
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

        private Material _coloredMaterial;
        public Material ColoredMaterial
        {
            get {
                if (_coloredMaterial != null)
                {
                    return _coloredMaterial;
                }

                _coloredMaterial = _themeNote.ColoredMaterialRenderer.materials[_themeNote.ColoredMaterialIndex];
                return _coloredMaterial;
            }
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

        public void SetColorWithEmission(Color c)
        {
            ColoredMaterial.color = c;
            ColoredMaterial.SetColor(_emissionColor, c * 8f);

            if (_themeNote.AddExtraGlow)
            {
                ColoredMaterial.color += new Color(3f, 3f, 3f);
            }
        }

        public Material[] GetAllMaterials() => _themeNote.ColoredMaterialRenderer.materials;

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