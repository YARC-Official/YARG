using System;
using System.Collections.Generic;
using UnityEngine;
using YARG.Core.Game;
using YARG.Helpers.Extensions;
using YARG.Settings;

namespace YARG.Gameplay.Visuals
{
    public class TrackMaterial : MonoBehaviour
    {
        // TODO: MOST OF THIS CLASS IS TEMPORARY UNTIL THE TRACK TEXTURE SETTINGS ARE IN

        private static readonly int _scrollProperty = Shader.PropertyToID("_Scroll");
        private static readonly int _starpowerStateProperty = Shader.PropertyToID("_Starpower_State");
        private static readonly int _wavinessProperty = Shader.PropertyToID("_Waviness");

        private static readonly int _layer1ColorProperty = Shader.PropertyToID("_Layer_1_Color");
        private static readonly int _layer2ColorProperty = Shader.PropertyToID("_Layer_2_Color");
        private static readonly int _layer3ColorProperty = Shader.PropertyToID("_Layer_3_Color");
        private static readonly int _layer4ColorProperty = Shader.PropertyToID("_Layer_4_Color");

        private static readonly int _soloStateProperty = Shader.PropertyToID("_Solo_State");

        private static readonly int _starPowerColorProperty = Shader.PropertyToID("_Starpower_Color");

        public struct Preset
        {
            public struct Layer
            {
                public Color Color;
            }

            public Layer Layer1;
            public Layer Layer2;
            public Layer Layer3;
            public Layer Layer4;

            public void SetFromProfile(ColorProfile colorProfile)
            {
                Layer1.Color = colorProfile.Common.GrooveColor1.ToUnityColor();
                Layer2.Color = colorProfile.Common.GrooveColor2.ToUnityColor();
                Layer3.Color = colorProfile.Common.GrooveColor3.ToUnityColor();
                Layer4.Color = colorProfile.Common.GrooveColor4.ToUnityColor();
            }
        }

        private static Preset _normalPreset;
        private static Preset _groovePreset;

        private float _grooveState;
        private float GrooveState
        {
            get => _grooveState;
            set
            {
                _grooveState = value;

                _material.SetColor(_layer1ColorProperty,
                    Color.Lerp(_normalPreset.Layer1.Color, _groovePreset.Layer1.Color, value));
                _material.SetColor(_layer2ColorProperty,
                    Color.Lerp(_normalPreset.Layer2.Color, _groovePreset.Layer2.Color, value));
                _material.SetColor(_layer3ColorProperty,
                    Color.Lerp(_normalPreset.Layer3.Color, _groovePreset.Layer3.Color, value));
                _material.SetColor(_layer4ColorProperty,
                    Color.Lerp(_normalPreset.Layer4.Color, _groovePreset.Layer4.Color, value));

                _material.SetFloat(_wavinessProperty, value);
            }
        }

        [HideInInspector]
        public bool GrooveMode;
        [HideInInspector]
        public bool StarpowerMode;

        private float _soloState;

        public float SoloState
        {
            get => _soloState;
            set
            {
                _soloState = value;

                foreach (var material in _trimMaterials)
                {
                    material.SetFloat(_soloStateProperty, value);
                }

                _material.SetFloat(_soloStateProperty, value);
            }
        }

        public float StarpowerState
        {
            get => _material.GetFloat(_starpowerStateProperty);
            set => _material.SetFloat(_starpowerStateProperty, value);
        }

        [SerializeField]
        private MeshRenderer _trackMesh;

        [SerializeField]
        private MeshRenderer[] _trackTrims;

        private Material _material;
        private readonly List<Material> _trimMaterials = new();

        private void Awake()
        {
            // Get materials
            _material = _trackMesh.material;
            foreach (var trim in _trackTrims)
            {
                _trimMaterials.Add(trim.material);
            }

            _normalPreset = new()
            {
                Layer1 = new()
                {
                    Color = FromHex("0F0F0F", 1f)
                },
                Layer2 = new()
                {
                    Color = FromHex("4B4B4B", 0.15f)
                },
                Layer3 = new()
                {
                    Color = FromHex("FFFFFF", 0f)
                },
                Layer4 = new()
                {
                    Color = FromHex("575757", 1f)
                }
            };

            _groovePreset = new()
            {
                Layer1 = new()
                {
                    Color = FromHex("000933", 1f)
                },
                Layer2 = new()
                {
                    Color = FromHex("23349C", 0.15f)
                },
                Layer3 = new()
                {
                    Color = FromHex("FFFFFF", 0f)
                },
                Layer4 = new()
                {
                    Color = FromHex("2C499E", 1f)
                }
            };
        }

        public void Initialize(float fadePos, float fadeSize, ColorProfile colorProfile)
        {
            // Set all fade values
            _material.SetFade(fadePos, fadeSize);
            foreach (var trimMat in _trimMaterials)
            {
                trimMat.SetFade(fadePos, fadeSize);
            }

            _material.SetColor(_starPowerColorProperty, colorProfile.Common.StarPowerColor.ToUnityColor() );
            _groovePreset.SetFromProfile(colorProfile);
        }

        private void Update()
        {
            if (GrooveMode)
            {
                GrooveState = Mathf.Lerp(GrooveState, 1f, Time.deltaTime * 5f);
            }
            else
            {
                GrooveState = Mathf.Lerp(GrooveState, 0f, Time.deltaTime * 3f);
            }

            if (StarpowerMode && SettingsManager.Settings.StarPowerHighwayFx.Value is StarPowerHighwayFxMode.On)
            {
                StarpowerState = Mathf.Lerp(StarpowerState, 1f, Time.deltaTime * 2f);
            }
            else
            {
                StarpowerState = Mathf.Lerp(StarpowerState, 0f, Time.deltaTime * 4f);
            }
        }

        private static Color FromHex(string hex, float alpha)
        {
            if (ColorUtility.TryParseHtmlString("#" + hex, out var color))
            {
                color.a = alpha;
                return color;
            }

            throw new InvalidOperationException();
        }

        public void SetTrackScroll(double time, float noteSpeed)
        {
            float position = (float) time * noteSpeed / 4f;
            _material.SetFloat(_scrollProperty, position);
        }
    }
}