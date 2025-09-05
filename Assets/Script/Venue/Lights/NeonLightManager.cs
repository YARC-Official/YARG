using UnityEngine;
using YARG.Core.Logging;

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
        private static readonly int _emissionColor = Shader.PropertyToID("_EmissionColor");

        [SerializeField]
        private Material[] _neonMaterials;
		
		[System.Serializable]
		public struct NeonFullColor {
			public Material Material;
			public VenueLightLocation Location;
			public VenueSpotLightLocation SpotLocation;
			[System.NonSerialized] 
			public Color InitialColor;
		}
		
		[SerializeField]
		private NeonFullColor[] _neonMaterialsFullColor;

        private LightManager _lightManager;

        private void Start()
        {
            _lightManager = FindObjectOfType<LightManager>();
			
			for (int i = 0; i < _neonMaterialsFullColor.Length; i++) {
				_neonMaterialsFullColor[i].InitialColor = (_neonMaterialsFullColor[i].Material.GetColor(_emissionColor));
			}
        }

        private void Update()
        {
            // Update all of the materials
            foreach (var material in _neonMaterials)
            {
				var lightState = _lightManager.GenericLightState;
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
			
            for (int i = 0; i < _neonMaterialsFullColor.Length; i++)
            {
				var neon = _neonMaterialsFullColor[i];
				
				switch ((neon.Location, neon.SpotLocation))
				{
					case (VenueLightLocation.Generic, VenueSpotLightLocation.None):
						var lightState = _lightManager.GenericLightState;
						neon.Material.SetColor(_emissionColor, lightState.Color ?? neon.InitialColor);
						neon.Material.SetFloat(_emissionMultiplier, lightState.Intensity);
					break;
					
					case (VenueLightLocation.Left, VenueSpotLightLocation.None):
						var lightStateLeft = _lightManager.LeftLightState;
						neon.Material.SetColor(_emissionColor, lightStateLeft.Color ?? neon.InitialColor);
						neon.Material.SetFloat(_emissionMultiplier, lightStateLeft.Intensity);
					break;
					
					case (VenueLightLocation.Right, VenueSpotLightLocation.None):
						var lightStateRight = _lightManager.RightLightState;
						neon.Material.SetColor(_emissionColor, lightStateRight.Color ?? neon.InitialColor);
						neon.Material.SetFloat(_emissionMultiplier, lightStateRight.Intensity);
					break;
					
					case (VenueLightLocation.Front, VenueSpotLightLocation.None):
						var lightStateFront = _lightManager.FrontLightState;
						neon.Material.SetColor(_emissionColor, lightStateFront.Color ?? neon.InitialColor);
						neon.Material.SetFloat(_emissionMultiplier, lightStateFront.Intensity);
					break;
					
					case (VenueLightLocation.Back, VenueSpotLightLocation.None):
						var lightStateBack = _lightManager.BackLightState;
						neon.Material.SetColor(_emissionColor, lightStateBack.Color ?? neon.InitialColor);
						neon.Material.SetFloat(_emissionMultiplier, lightStateBack.Intensity);
					break;
					
					case (VenueLightLocation.Center, VenueSpotLightLocation.None):
						var lightStateCenter = _lightManager.CenterLightState;
						neon.Material.SetColor(_emissionColor, lightStateCenter.Color ?? neon.InitialColor);
						neon.Material.SetFloat(_emissionMultiplier, lightStateCenter.Intensity);
					break;
					
					case (VenueLightLocation.Crowd, VenueSpotLightLocation.None):
						var lightStateCrowd = _lightManager.CrowdLightState;
						neon.Material.SetColor(_emissionColor, lightStateCrowd.Color ?? neon.InitialColor);
						neon.Material.SetFloat(_emissionMultiplier, lightStateCrowd.Intensity);
					break;
					
					case (_, VenueSpotLightLocation.Bass):
						var BassIntensity = neon.Material.GetFloat(_emissionMultiplier);
						var lightStateBass = _lightManager.GetSpotlightStateFor(VenueSpotLightLocation.Bass);
						float Bass = Mathf.Lerp(BassIntensity, lightStateBass ? 1f : 0f, Time.deltaTime * 10f);
						neon.Material.SetFloat(_emissionMultiplier, Bass);
					break;
					
					case (_, VenueSpotLightLocation.Drums):
						var DrumsIntensity = neon.Material.GetFloat(_emissionMultiplier);
						var lightStateDrums = _lightManager.GetSpotlightStateFor(VenueSpotLightLocation.Drums);
						float Drums = Mathf.Lerp(DrumsIntensity, lightStateDrums ? 1f : 0f, Time.deltaTime * 10f);
						neon.Material.SetFloat(_emissionMultiplier, Drums);
					break;
					
					case (_, VenueSpotLightLocation.Guitar):
						var GuitarIntensity = neon.Material.GetFloat(_emissionMultiplier);
						var lightStateGuitar = _lightManager.GetSpotlightStateFor(VenueSpotLightLocation.Guitar);
						float Guitar = Mathf.Lerp(GuitarIntensity, lightStateGuitar ? 1f : 0f, Time.deltaTime * 10f);
						neon.Material.SetFloat(_emissionMultiplier, Guitar);
					break;
					
					case (_, VenueSpotLightLocation.Vocals):
						var VocalsIntensity = neon.Material.GetFloat(_emissionMultiplier);
						var lightStateVocals = _lightManager.GetSpotlightStateFor(VenueSpotLightLocation.Vocals);
						float Vocals = Mathf.Lerp(VocalsIntensity, lightStateVocals ? 1f : 0f, Time.deltaTime * 10f);
						neon.Material.SetFloat(_emissionMultiplier, Vocals);
					break;
					
					default:
						YargLogger.LogDebug("Unknown location for neon light");
					break;
				}
            }
        }
    }
}
