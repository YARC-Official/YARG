using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using YARG.Core.Game;
using YARG.Helpers.Extensions;
using YARG.Settings;
using YARG.Settings.Customization;

namespace YARG.Gameplay.Visuals
{
    public class TrackMaterial : MonoBehaviour
    {
        // TODO: MOST OF THIS CLASS IS TEMPORARY UNTIL THE TRACK TEXTURE SETTINGS ARE IN

        private static readonly int _scrollProperty         = Shader.PropertyToID("_Scroll");
        private static readonly int _starpowerStateProperty = Shader.PropertyToID("_Starpower_State");
        private static readonly int _starpowerTimeProperty  = Shader.PropertyToID("_Starpower_Start_Time");
        private static readonly int _wavinessProperty       = Shader.PropertyToID("_Waviness");

        private static readonly int _layer1ColorProperty = Shader.PropertyToID("_Layer_1_Color");
        private static readonly int _layer2ColorProperty = Shader.PropertyToID("_Layer_2_Color");
        private static readonly int _layer3ColorProperty = Shader.PropertyToID("_Layer_3_Color");
        private static readonly int _layer4ColorProperty = Shader.PropertyToID("_Layer_4_Color");

        private static readonly int _starPowerColorProperty = Shader.PropertyToID("_Starpower_Color");

        private static readonly int _baseTextureProperty = Shader.PropertyToID("_Layer_2_Texture");
        private static readonly int _baseParallaxProperty = Shader.PropertyToID("_Layer_2_Parallax");
        private static readonly int _baseWavinessProperty = Shader.PropertyToID("_Layer_2_Wavy_Amount");
        private static readonly int _sidePatternProperty = Shader.PropertyToID("_Layer_4_Texture");
        private static readonly int _sideParallaxProperty = Shader.PropertyToID("_Layer_4_Parallax");
        private static readonly int _sideWavinessProperty = Shader.PropertyToID("_Layer_4_Wavy_Amount");

        private Texture _originalBaseTexture;
        private Texture _originalSidePattern;
        private float   _originalBaseParallax;
        private float   _originalSideParallax;
        private float   _originalBaseWaviness;
        private float   _originalSideWaviness;

        public struct Preset
        {
            public Color    Layer1;
            public Color    Layer2;
            public Color    Layer3;
            public Color    Layer4;
            public FileInfo BaseTexture;
            public FileInfo SidePattern;
            public float    BaseWaviness;
            public float    SideWaviness;

            public static Preset FromHighwayPreset(HighwayPreset preset, bool groove)
            {
                if (groove)
                {
                    return new Preset
                    {
                        Layer1 = preset.BackgroundGrooveBaseColor1.ToUnityColor(),
                        Layer2 = preset.BackgroundGrooveBaseColor2.ToUnityColor(),
                        Layer3 = preset.BackgroundGrooveBaseColor3.ToUnityColor(),
                        Layer4 = preset.BackgroundGroovePatternColor.ToUnityColor(),
                        BaseTexture = preset.BackgroundImage,
                        SidePattern = preset.SideImage,
                        BaseWaviness = preset.BaseWaviness,
                        SideWaviness = preset.SideWaviness
                    };
                }

                return new Preset
                {
                    Layer1 = preset.BackgroundBaseColor1.ToUnityColor(),
                    Layer2 = preset.BackgroundBaseColor2.ToUnityColor(),
                    Layer3 = preset.BackgroundBaseColor3.ToUnityColor(),
                    Layer4 = preset.BackgroundPatternColor.ToUnityColor(),
                    BaseTexture = preset.BackgroundImage,
                    SidePattern = preset.SideImage,
                    BaseWaviness = preset.BaseWaviness,
                    SideWaviness = preset.SideWaviness
                };
            }
        }

        private Preset _normalPreset;
        private Preset _groovePreset;

        private float _grooveState;
        private float GrooveState
        {
            get => _grooveState;
            set
            {
                _grooveState = value;

                _material.SetColor(_layer1ColorProperty,
                    Color.Lerp(_normalPreset.Layer1, _groovePreset.Layer1, value));
                _material.SetColor(_layer2ColorProperty,
                    Color.Lerp(_normalPreset.Layer2, _groovePreset.Layer2, value));
                _material.SetColor(_layer3ColorProperty,
                    Color.Lerp(_normalPreset.Layer3, _groovePreset.Layer3, value));
                _material.SetColor(_layer4ColorProperty,
                    Color.Lerp(_normalPreset.Layer4, _groovePreset.Layer4, value));

                _material.SetFloat(_wavinessProperty, value);
            }
        }

        [HideInInspector]
        public bool GrooveMode;

        private bool _starpowerMode;
        [HideInInspector]
        public bool StarpowerMode
        {
            get => _starpowerMode;
            // When going from false to true, also set the starpower time
            set
            {
                if (value && !_starpowerMode)
                {
                    _material.SetFloat(_starpowerTimeProperty, Time.time);
                }
                _starpowerMode = value;
            }
        }
        private GameManager _gameManager;

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

        private Texture2D _baseTexture;
        private Texture2D _sidePattern;

        private const string BASE_TEXTURE_NAME = "baseTexture.png";
        private const string SIDE_PATTERN_NAME = "sidePattern.png";

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
                Layer1 = FromHex("0F0F0F", 1f),
                Layer2 = FromHex("4B4B4B", 0.15f),
                Layer3 = FromHex("FFFFFF", 0f),
                Layer4 = FromHex("575757", 1f)
            };

            _groovePreset = new()
            {
                Layer1 = FromHex("000933", 1f),
                Layer2 = FromHex("23349C", 0.15f),
                Layer3 = FromHex("FFFFFF", 0f),
                Layer4 = FromHex("2C499E", 1f)
            };

            // If the original textures haven't been saved yet, save them now
            if (_originalBaseTexture == null)
            {
                _originalBaseTexture = _material.GetTexture(_baseTextureProperty);
                _originalBaseParallax = _material.GetFloat(_baseParallaxProperty);
                _originalBaseWaviness = _material.GetFloat(_baseWavinessProperty);
                _originalSidePattern = _material.GetTexture(_sidePatternProperty);
                _originalSideParallax = _material.GetFloat(_sideParallaxProperty);
                _originalSideWaviness = _material.GetFloat(_sideWavinessProperty);
            }
        }

        public void Initialize(HighwayPreset highwayPreset)
        {
            _material.SetColor(_starPowerColorProperty, highwayPreset.StarPowerColor.ToUnityColor() );
            _normalPreset = Preset.FromHighwayPreset(highwayPreset, false);
            _groovePreset = Preset.FromHighwayPreset(highwayPreset, true);

            // Waviness applies whether or not custom textures are used
            _material.SetFloat(_baseWavinessProperty, _normalPreset.BaseWaviness);
            _material.SetFloat(_sideWavinessProperty, _normalPreset.SideWaviness);

            SetTextures();
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
                StarpowerState = 1.0f;
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

        // TODO: Integrate this with CustomContentManager so it can be managed in settings
        private void SetTextures()
        {
            if (_normalPreset.BaseTexture == null || _normalPreset.SidePattern == null)
            {
                SetDefaultTextures();
                return;
            }

            var baseTexturePath = _normalPreset.BaseTexture.FullName;
            var sidePatternPath = _normalPreset.SidePattern.FullName;

            if (!File.Exists(baseTexturePath) || !File.Exists(sidePatternPath))
            {
                // Use default textures
                SetDefaultTextures();
                return;
            }

            var bytes = File.ReadAllBytes(baseTexturePath);
            _baseTexture = new Texture2D(2, 2);
            var success = _baseTexture.LoadImage(bytes);

            bytes = File.ReadAllBytes(sidePatternPath);
            _sidePattern = new Texture2D(2, 2);
            success = success && _sidePattern.LoadImage(bytes);

            // If either didn't load, use defaults
            if (!success)
            {
                SetDefaultTextures();
                return;
            }

            _material.SetTexture(_baseTextureProperty, _baseTexture);
            _material.SetFloat(_baseParallaxProperty, 1f);
            _material.SetTexture(_sidePatternProperty, _sidePattern);
            _material.SetFloat(_sideParallaxProperty, 1f);
        }

        private void SetDefaultTextures()
        {
            _material.SetTexture(_baseTextureProperty, _originalBaseTexture);
            _material.SetFloat(_baseParallaxProperty, _originalBaseParallax);
            _material.SetTexture(_sidePatternProperty, _originalSidePattern);
            _material.SetFloat(_sideParallaxProperty, _originalSideParallax);
        }
    }
}
