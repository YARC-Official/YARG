﻿using UnityEngine;
using YARG.Helpers.Extensions;
using YARG.Themes;

namespace YARG.Gameplay.Visuals
{
    public class NoteGroup : MonoBehaviour
    {
        private static readonly int _emissionColor = Shader.PropertyToID("_EmissionColor");

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

        public void SetColorWithEmission(Color c)
        {
            ColoredMaterial.color = c;
            ColoredMaterial.SetColor(_emissionColor, c * 8f);
        }

        public Material[] GetAllMaterials() => _meshRenderer.materials;

        public void SetActive(bool a) => gameObject.SetActive(a);

        public void SetModelFromTheme(GameObject model)
        {
            // Copy the model
            var copy = Instantiate(model, transform);
            copy.transform.localPosition = Vector3.zero;

            // Set new information
            var themeNote = copy.GetComponent<ThemeNote>();
            _meshRenderer = themeNote.ColoredMaterialRenderer;
            _coloredMaterialIndex = themeNote.ColoredMaterialIndex;

            // Clean up
            Destroy(themeNote);
        }
    }
}