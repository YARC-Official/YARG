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
		[SerializeField]
		private Material[] _neonLeftMaterials;
		[SerializeField]
		private Material[] _neonRightMaterials;
		[SerializeField]
		private Material[] _neonFrontMaterials;
		[SerializeField]
		private Material[] _neonBackMaterials;
		[SerializeField]
		private Material[] _neonCenterMaterials;
		[SerializeField]
		private Material[] _neonCrowdMaterials;

        private LightManager _lightManager;
		private Color _defaultColorLeft;
		private Color _defaultColorRight;
		private Color _defaultColorFront;
		private Color _defaultColorBack;
		private Color _defaultColorCenter;
		private Color _defaultColorCrowd;

        private void Awake()
        {
            _lightManager = FindObjectOfType<LightManager>();
			
			foreach (var material in _neonLeftMaterials)
			{
				_defaultColorLeft = material.GetColor(_emissionColor);
			}
			
			foreach (var material in _neonRightMaterials)
			{
				_defaultColorRight = material.GetColor(_emissionColor);
			}
			
			foreach (var material in _neonFrontMaterials)
			{
				_defaultColorFront = material.GetColor(_emissionColor);
			}
			
			foreach (var material in _neonBackMaterials)
			{
				_defaultColorBack = material.GetColor(_emissionColor);
			}
			
			foreach (var material in _neonCenterMaterials)
			{
				_defaultColorCenter = material.GetColor(_emissionColor);
			}
			
			foreach (var material in _neonCrowdMaterials)
			{
				_defaultColorCrowd = material.GetColor(_emissionColor);
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
			
			

            foreach (var material in _neonLeftMaterials)
            {
                var lightState = _lightManager.LeftLightState;
				material.SetFloat(_emissionMultiplier, lightState.Intensity);

                if (lightState.Color == null)
                {
                    material.SetColor(_emissionColor, _defaultColorLeft);
                }
                else
                {
					material.SetColor(_emissionColor, lightState.Color.Value);
                }
            }
			
			

            foreach (var material in _neonRightMaterials)
            {
                var lightState = _lightManager.RightLightState;
				material.SetFloat(_emissionMultiplier, lightState.Intensity);

                if (lightState.Color == null)
                {
                    material.SetColor(_emissionColor, _defaultColorRight);
                }
                else
                {
					material.SetColor(_emissionColor, lightState.Color.Value);
                }
            }
			

            foreach (var material in _neonFrontMaterials)
            {
				var lightState = _lightManager.FrontLightState;
				material.SetFloat(_emissionMultiplier, lightState.Intensity);

                if (lightState.Color == null)
                {
                    material.SetColor(_emissionColor, _defaultColorFront);
                }
                else
                {
					material.SetColor(_emissionColor, lightState.Color.Value);
                }
            }
			
			

            foreach (var material in _neonBackMaterials)
            {
                var lightState = _lightManager.BackLightState;
				material.SetFloat(_emissionMultiplier, lightState.Intensity);

                if (lightState.Color == null)
                {
                    material.SetColor(_emissionColor, _defaultColorBack);
                }
                else
                {
					material.SetColor(_emissionColor, lightState.Color.Value);
                }
            }
			
			

            foreach (var material in _neonCenterMaterials)
            {
                var lightState = _lightManager.CenterLightState;
				material.SetFloat(_emissionMultiplier, lightState.Intensity);

                if (lightState.Color == null)
                {
                    material.SetColor(_emissionColor, _defaultColorCenter);
                }
                else
                {
					material.SetColor(_emissionColor, lightState.Color.Value);
                }
			}
			
			

            foreach (var material in _neonCrowdMaterials)
            {
                var lightState = _lightManager.CrowdLightState;
				material.SetFloat(_emissionMultiplier, lightState.Intensity);

                if (lightState.Color == null)
                {
                    material.SetColor(_emissionColor, _defaultColorCrowd);
                }
                else
                {
					material.SetColor(_emissionColor, lightState.Color.Value);
                }
            }
        }
    }
}
