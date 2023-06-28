using UnityEngine;

namespace YARG.Venue
{
    public class NeonLightManager : MonoBehaviour
    {
        private static readonly int EmissionMultiplier = Shader.PropertyToID("_Emission_Multiplier");
        private static readonly int EmissionSecondaryColor = Shader.PropertyToID("_Emission_Secondary_Color");

        [SerializeField]
        private Material[] _neonMaterials;

        private void Update()
        {
            var lightState = LightManager.Instance.MainLightState;

            // Update all of the materials
            foreach (var material in _neonMaterials)
            {
                material.SetFloat(EmissionMultiplier, lightState.Intensity);

                if (lightState.Color == null)
                {
                    material.SetColor(EmissionSecondaryColor, Color.white);
                }
                else
                {
                    material.SetColor(EmissionSecondaryColor, lightState.Color.Value);
                }
            }
        }
    }
}