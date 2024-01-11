using UnityEngine;

namespace YARG.Venue
{
    // WARNING: Changing this could break themes or venues!
    //
    // This script is used a lot in theme creation.
    // Changing the serialized fields in this file will result in older themes
    // not working properly. Only change if you need to.

    public class NeonLightManager : MonoBehaviour
    {
        private static readonly int _emissionMultiplier = Shader.PropertyToID("_Emission_Multiplier");
        private static readonly int _emissionSecondaryColor = Shader.PropertyToID("_Emission_Secondary_Color");

        [SerializeField]
        private Material[] _neonMaterials;

        private LightManager _lightManager;

        private void Awake()
        {
            _lightManager = FindObjectOfType<LightManager>();
        }

        private void Update()
        {
            var lightState = _lightManager.MainLightState;

            // Update all of the materials
            foreach (var material in _neonMaterials)
            {
                material.SetFloat(_emissionMultiplier, lightState.Intensity);

                if (lightState.Color == null)
                {
                    material.SetColor(_emissionSecondaryColor, Color.white);
                }
                else
                {
                    material.SetColor(_emissionSecondaryColor, lightState.Color.Value);
                }
            }
        }
    }
}