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
				
				switch (neon.Location)
				{
					case VenueLightLocation.Generic:
						var lightState = _lightManager.GenericLightState;
						neon.Material.SetColor(_emissionColor, lightState.Color ?? neon.InitialColor);
						neon.Material.SetFloat(_emissionMultiplier, lightState.Intensity);
					break;
					
					case VenueLightLocation.Left:
						var lightStateLeft = _lightManager.LeftLightState;
						neon.Material.SetColor(_emissionColor, lightStateLeft.Color ?? neon.InitialColor);
						neon.Material.SetFloat(_emissionMultiplier, lightStateLeft.Intensity);
					break;
					
					case VenueLightLocation.Right:
						var lightStateRight = _lightManager.RightLightState;
						neon.Material.SetColor(_emissionColor, lightStateRight.Color ?? neon.InitialColor);
						neon.Material.SetFloat(_emissionMultiplier, lightStateRight.Intensity);
					break;
					
					case VenueLightLocation.Front:
						var lightStateFront = _lightManager.FrontLightState;
						neon.Material.SetColor(_emissionColor, lightStateFront.Color ?? neon.InitialColor);
						neon.Material.SetFloat(_emissionMultiplier, lightStateFront.Intensity);
					break;
					
					case VenueLightLocation.Back:
						var lightStateBack = _lightManager.BackLightState;
						neon.Material.SetColor(_emissionColor, lightStateBack.Color ?? neon.InitialColor);
						neon.Material.SetFloat(_emissionMultiplier, lightStateBack.Intensity);
					break;
					
					case VenueLightLocation.Center:
						var lightStateCenter = _lightManager.CenterLightState;
						neon.Material.SetColor(_emissionColor, lightStateCenter.Color ?? neon.InitialColor);
						neon.Material.SetFloat(_emissionMultiplier, lightStateCenter.Intensity);
					break;
					
					case VenueLightLocation.Crowd:
						var lightStateCrowd = _lightManager.CrowdLightState;
						neon.Material.SetColor(_emissionColor, lightStateCrowd.Color ?? neon.InitialColor);
						neon.Material.SetFloat(_emissionMultiplier, lightStateCrowd.Intensity);
					break;
					
					default:
						YargLogger.LogDebug("Unknown location for neon light");
					break;
				}
            }
        }
    }
}
