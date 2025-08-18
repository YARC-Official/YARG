using System.Collections.Generic;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Core.Extensions;
using YARG.Gameplay;
using YARG.Playback;
using Random = UnityEngine.Random;

namespace YARG.Venue
{
    public partial class LightManager : GameplayBehaviour
    {
        public struct LightState
        {
            /// <summary>
            /// The intensity of the light between <c>0</c> and <c>1</c>. <c>1</c> is the default value.
            /// </summary>
            public float Intensity;

            /// <summary>
            /// The color of the light. <see cref="Intensity"/> should be taken into consideration.
            /// <c>null</c> indicates default.
            /// </summary>
            public Color? Color;

            public float Delta;
        }

        public LightingType Animation { get; private set; }
        public int AnimationFrame { get; private set; }

        private LightState[] _lightStates;
        public LightState GenericLightState => _lightStates[(int) VenueLightLocation.Generic];

        [SerializeField]
        private float _gradientLightingSpeed = 0.125f;
        [SerializeField]
        private float _gradientRandomness = 0.5f;

        [Space]
        [SerializeField]
        private Color[] _warmColors;
        [SerializeField]
        private Color[] _coolColors;
        [SerializeField]
        private Color[] _dissonantColors;
        [SerializeField]
        private Color[] _harmoniousColors;
        [SerializeField]
        private Color _silhouetteColor;

        private List<LightingEvent> _lightingEvents;

        private Gradient _warmGradient;
        private Gradient _coolGradient;
        private Gradient _dissonantGradient;
        private Gradient _harmoniousGradient;

        private int _lightingEventIndex;
        private int _beatIndex;

        protected override void OnChartLoaded(SongChart chart)
        {
            _lightStates = new LightState[EnumExtensions<VenueLightLocation>.Count];

            _lightingEvents = chart.VenueTrack.Lighting;

            // If the color arrays are empty, add basic ones for safety

            if (_warmColors is not { Length: > 0 })
            {
                _warmColors = new[]
                {
                    Color.red,
                    Color.yellow
                };
            }

            if (_coolColors is not { Length: > 0 })
            {
                _coolColors = new[]
                {
                    Color.blue,
                    Color.green
                };
            }

            if (_dissonantColors is not { Length: > 0 })
            {
                _dissonantColors = new[]
                {
                    Color.red,
                    Color.green,
                    Color.blue,
                };
            }

            if (_harmoniousColors is not { Length: > 0 })
            {
                _harmoniousColors = new[]
                {
                    Color.yellow,
                    Color.red,
                    Color.blue,
                };
            }

            // Setup gradients
            _warmGradient = CreateGradient(_warmColors);
            _coolGradient = CreateGradient(_coolColors);
            _dissonantGradient = CreateGradient(_dissonantColors);
            _harmoniousGradient = CreateGradient(_harmoniousColors);

            // 1/8th of a beat is a 32nd note
            GameManager.BeatEventHandler.Visual.Subscribe(UpdateLightAnimation, BeatEventType.QuarterNote, division: 1f / 8f);
        }

        protected override void GameplayDestroy()
        {
            GameManager.BeatEventHandler.Visual.Unsubscribe(UpdateLightAnimation);
        }

        private void Update()
        {
            // Look for new lighting events
            while (_lightingEventIndex < _lightingEvents.Count &&
                _lightingEvents[_lightingEventIndex].Time <= GameManager.VisualTime)
            {
                var current = _lightingEvents[_lightingEventIndex];

                switch (current.Type)
                {
                    case LightingType.KeyframeNext:
                        AnimationFrame++;
                        break;
                    case LightingType.KeyframePrevious:
                        AnimationFrame--;
                        break;
                    case LightingType.KeyframeFirst:
                        AnimationFrame = 0;
                        break;
                    case LightingType.WarmAutomatic:
                    case LightingType.WarmManual:
                    case LightingType.CoolAutomatic:
                    case LightingType.CoolManual:
                    case LightingType.Verse:
                    case LightingType.Chorus:
                        // Add a slight randomness to colored cues
                        for (int i = 0; i < _lightStates.Length; i++)
                        {
                            _lightStates[i].Delta = Random.Range(0f, _gradientRandomness);
                        }

                        goto default;
                    default:
                        Animation = current.Type;
                        AnimationFrame = 0;
                        break;
                }

                _lightingEventIndex++;
            }

            UpdateLightStates();
        }

        private void UpdateLightAnimation()
        {
            _beatIndex++;

            switch (Animation)
            {
                case LightingType.StrobeFast:
                    AnimationFrame++;
                    break;
                case LightingType.StrobeSlow:
                    if (_beatIndex % 2 == 1)
                    {
                        AnimationFrame++;
                    }

                    break;
            }
        }

        private void UpdateLightStates()
        {
            for (int i = 0; i < _lightStates.Length; i++)
            {
                var location = (VenueLightLocation) i;

                switch (Animation)
                {
                    case LightingType.Verse:
                        _lightStates[i] = AutoGradientSplit(_lightStates[i], location, _coolGradient, _warmGradient);
                        break;
                    case LightingType.Chorus:
                        _lightStates[i] = AutoGradientSplit(_lightStates[i], location, _warmGradient, _coolGradient);
                        break;
                    case LightingType.BlackoutFast:
                        _lightStates[i] = BlackOut(_lightStates[i], 15f);
                        break;
                    case LightingType.BlackoutSlow:
                        _lightStates[i] = BlackOut(_lightStates[i], 10f);
                        break;
                    case LightingType.BigRockEnding:
                    case LightingType.Dischord:
                    case LightingType.Frenzy:
                        _lightStates[i] = AutoGradient(_lightStates[i], _dissonantGradient);
                        break;
                    case LightingType.CoolAutomatic:
                    case LightingType.CoolManual:
                        _lightStates[i] = AutoGradient(_lightStates[i], _coolGradient);
                        break;
                    case LightingType.FlareFast:
                        _lightStates[i] = Flare(_lightStates[i], 15f);
                        break;
                    case LightingType.FlareSlow:
                        _lightStates[i] = Flare(_lightStates[i], 10f);
                        break;
                    case LightingType.Harmony:
                        _lightStates[i] = AutoGradient(_lightStates[i], _harmoniousGradient);
                        break;
                    case LightingType.Silhouettes:
                    case LightingType.SilhouettesSpotlight:
                        _lightStates[i] = Silhouette(_lightStates[i], location);
                        break;
                    case LightingType.StrobeFast:
                    case LightingType.StrobeSlow:
                    case LightingType.Stomp:
                        _lightStates[i] = Strobe(_lightStates[i]);
                        break;
                    case LightingType.WarmAutomatic:
                    case LightingType.WarmManual:
                        _lightStates[i] = AutoGradient(_lightStates[i], _warmGradient);
                        break;
                    default:
                        _lightStates[i].Intensity = 1f;
                        _lightStates[i].Color = null;
                        _lightStates[i].Delta = 0f;
                        break;
                }
            }
        }

        public LightState GetLightStateFor(VenueLightLocation location)
        {
            return _lightStates[(int) location];
        }

        private static Gradient CreateGradient(Color[] colors)
        {
            var gradient = new Gradient();

            var keys = new GradientColorKey[colors.Length + 1];

            // Make the gradient loop nice without snapping
            keys[0] = new GradientColorKey(colors[^1], 0f);

            // Add the rest of the colors
            for (int i = 1; i < keys.Length; i++)
            {
                keys[i] = new GradientColorKey(colors[i - 1], 1f / colors.Length * i);
            }

            // No alpha for gradient
            gradient.SetKeys(keys, new[]
            {
                new GradientAlphaKey(1f, 0f)
            });

            return gradient;
        }
    }
}