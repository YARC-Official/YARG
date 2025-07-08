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
        private static readonly int _emissionColor = Shader.PropertyToID("_EmissionColor");

        [SerializeField]
        private Material[] _neonMaterials;
		
		[System.Serializable]
		public class NeonFullColor {
			public Material Material;
			public VenueLightLocation Location;
			[System.NonSerialized] 
			public Color InitialColor;
		}
		
		[SerializeField]
		private NeonFullColor[] _neonMaterialsFullColor;

        private LightManager _lightManager;

        private void Awake()
        {
            _lightManager = FindObjectOfType<LightManager>();
			
			for (int i = 0; i < _neonMaterialsFullColor.Length; i++) {
				_neonMaterialsFullColor[i].InitialColor = (_neonMaterialsFullColor[i].Material.GetColor(_emissionColor));
			}
        }

        private void Update()
        {
			var lightState = _lightManager.GenericLightState;
			var lightStateLeft = _lightManager.LeftLightState;
			var lightStateRight = _lightManager.RightLightState;
			var lightStateFront = _lightManager.FrontLightState;
			var lightStateBack = _lightManager.BackLightState;
			var lightStateCenter = _lightManager.CenterLightState;
			var lightStateCrowd = _lightManager.CrowdLightState;

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
			
            for (int i = 0; i < _neonMaterialsFullColor.Length; i++) 
                {
                if (_neonMaterialsFullColor[i].Location == VenueLightLocation.Generic)
                {
					_neonMaterialsFullColor[i].Material.SetColor(_emissionColor, lightState.Color ?? _neonMaterialsFullColor[i].InitialColor);
					_neonMaterialsFullColor[i].Material.SetFloat(_emissionMultiplier, lightState.Intensity);
                }
				else if (_neonMaterialsFullColor[i].Location == VenueLightLocation.Left)
                {
					_neonMaterialsFullColor[i].Material.SetColor(_emissionColor, lightStateLeft.Color ?? _neonMaterialsFullColor[i].InitialColor);
					_neonMaterialsFullColor[i].Material.SetFloat(_emissionMultiplier, lightStateLeft.Intensity);
                }
                else if (_neonMaterialsFullColor[i].Location == VenueLightLocation.Right)
                {
					_neonMaterialsFullColor[i].Material.SetColor(_emissionColor, lightStateRight.Color ?? _neonMaterialsFullColor[i].InitialColor);
					_neonMaterialsFullColor[i].Material.SetFloat(_emissionMultiplier, lightStateRight.Intensity);
                }
                else if (_neonMaterialsFullColor[i].Location == VenueLightLocation.Front)
                {
					_neonMaterialsFullColor[i].Material.SetColor(_emissionColor, lightStateFront.Color ?? _neonMaterialsFullColor[i].InitialColor);
					_neonMaterialsFullColor[i].Material.SetFloat(_emissionMultiplier, lightStateFront.Intensity);
                }
                else if (_neonMaterialsFullColor[i].Location == VenueLightLocation.Back)
                {
					_neonMaterialsFullColor[i].Material.SetColor(_emissionColor, lightStateBack.Color ?? _neonMaterialsFullColor[i].InitialColor);
					_neonMaterialsFullColor[i].Material.SetFloat(_emissionMultiplier, lightStateBack.Intensity);
                }
                else if (_neonMaterialsFullColor[i].Location == VenueLightLocation.Center)
                {
					_neonMaterialsFullColor[i].Material.SetColor(_emissionColor, lightStateCenter.Color ?? _neonMaterialsFullColor[i].InitialColor);
					_neonMaterialsFullColor[i].Material.SetFloat(_emissionMultiplier, lightStateCenter.Intensity);
                }
                else if (_neonMaterialsFullColor[i].Location == VenueLightLocation.Crowd)
                {
					_neonMaterialsFullColor[i].Material.SetColor(_emissionColor, lightStateCrowd.Color ?? _neonMaterialsFullColor[i].InitialColor);
					_neonMaterialsFullColor[i].Material.SetFloat(_emissionMultiplier, lightStateCrowd.Intensity);
                }
            }
        }
    }
}
